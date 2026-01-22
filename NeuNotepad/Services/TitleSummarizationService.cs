using Microsoft.ML.OnnxRuntimeGenAI;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NeuNotepad.Services;

/// <summary>
/// Service for generating smart titles from document content using a local SLM (Phi-3).
/// </summary>
public class TitleSummarizationService : IDisposable
{
    private Model? _model;
    private Tokenizer? _tokenizer;
    private readonly AISettings _settings;
    private readonly object _lock = new();
    private bool _isInitialized;
    private bool _initializationFailed;
    private string? _initializationError;

    // Cache to avoid re-summarizing unchanged content
    private readonly Dictionary<string, string> _titleCache = new();

    public TitleSummarizationService(AISettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Whether the service is available (model loaded successfully).
    /// </summary>
    public bool IsAvailable => _isInitialized && !_initializationFailed;

    /// <summary>
    /// Error message if initialization failed.
    /// </summary>
    public string? InitializationError => _initializationError;

    /// <summary>
    /// Event raised when initialization status changes.
    /// </summary>
    public event Action<bool, string?>? InitializationStatusChanged;

    /// <summary>
    /// Initialize the model asynchronously. Call this before using GenerateTitleAsync.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized || _initializationFailed)
            return;

        if (string.IsNullOrEmpty(_settings.ModelPath) || !Directory.Exists(_settings.ModelPath))
        {
            _initializationFailed = true;
            _initializationError = "Model path not configured or does not exist.";
            InitializationStatusChanged?.Invoke(false, _initializationError);
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (_isInitialized) return;

                    _model = new Model(_settings.ModelPath);
                    _tokenizer = new Tokenizer(_model);
                    _isInitialized = true;
                }
            });

            InitializationStatusChanged?.Invoke(true, null);
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            _initializationError = $"Failed to load model: {ex.Message}";
            InitializationStatusChanged?.Invoke(false, _initializationError);
        }
    }

    /// <summary>
    /// Generate a short, descriptive title for the given content.
    /// </summary>
    /// <param name="content">The document content to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A short title, or null if generation fails.</returns>
    public async Task<string?> GenerateTitleAsync(string content, CancellationToken cancellationToken = default)
    {
        if (!_settings.SmartTitlesEnabled)
            return null;

        if (!_isInitialized)
        {
            await InitializeAsync();
            if (!IsAvailable)
                return null;
        }

        if (string.IsNullOrWhiteSpace(content))
            return null;

        // Truncate content if too long
        var truncatedContent = content.Length > _settings.MaxContentLength
            ? content[.._settings.MaxContentLength] + "..."
            : content;

        // Check cache
        var contentHash = ComputeHash(truncatedContent);
        lock (_titleCache)
        {
            if (_titleCache.TryGetValue(contentHash, out var cachedTitle))
                return cachedTitle;
        }

        try
        {
            var title = await Task.Run(() => GenerateTitle(truncatedContent), cancellationToken);
            
            if (!string.IsNullOrEmpty(title))
            {
                lock (_titleCache)
                {
                    _titleCache[contentHash] = title;
                    
                    // Limit cache size
                    if (_titleCache.Count > 100)
                    {
                        var oldestKey = _titleCache.Keys.First();
                        _titleCache.Remove(oldestKey);
                    }
                }
            }

            return title;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _initializationError = $"Generation failed: {ex.Message}";
            return null;
        }
    }

    private string? GenerateTitle(string content)
    {
        if (_model == null || _tokenizer == null)
        {
            _initializationError = "Model or tokenizer not initialized";
            return null;
        }

        try
        {
            // Preprocess content: take first meaningful chunk and clean it up
            var processedContent = PreprocessContent(content);
            
            // Craft prompt for title generation using few-shot examples for better guidance
            var prompt = $"""
                <|system|>
                You are a helpful assistant that creates short, descriptive titles for documents. 
                Create a title that captures the main purpose or topic, not just the first words.
                <|end|>
                <|user|>
                Create a short title (3-5 words) for this text. The title should describe what the document is about, not copy words from it.

                Example 1:
                Text: "def calculate_sum(a, b): return a + b"
                Title: Python Addition Function

                Example 2:
                Text: "Meeting scheduled for Monday at 10am to discuss Q4 budget allocations and team performance reviews"
                Title: Q4 Budget Meeting Notes

                Example 3:
                Text: "Dear Customer, Thank you for your purchase. Your order #12345 has been shipped."
                Title: Order Shipment Confirmation

                Now create a title for this text:
                {processedContent}
                <|end|>
                <|assistant|>
                Title:
                """;

            var sequences = _tokenizer.Encode(prompt);

            using var generatorParams = new GeneratorParams(_model);
            generatorParams.SetSearchOption("max_length", 768); // Input + output length (support larger content)
            generatorParams.SetSearchOption("temperature", 0.5); // Slightly higher for more creative titles
            generatorParams.SetSearchOption("top_p", 0.9); // Nucleus sampling for better quality
            generatorParams.SetInputSequences(sequences);

            var outputTokens = new List<int>();
            using var generator = new Generator(_model, generatorParams);
            
            while (!generator.IsDone())
            {
                generator.ComputeLogits();
                generator.GenerateNextToken();
                
                var token = generator.GetSequence(0)[^1];
                outputTokens.Add(token);
                
                // Decode incrementally to check for end markers
                var partialResult = _tokenizer.Decode(outputTokens.ToArray());
                
                // Stop if we hit end markers or newline (title complete)
                if (partialResult.Contains("<|end|>") || 
                    partialResult.Contains("<|user|>") ||
                    partialResult.Contains('\n') ||
                    outputTokens.Count > 30)
                    break;
            }

            var result = _tokenizer.Decode(outputTokens.ToArray());
            
            // Clean up the result
            result = CleanTitle(result);
            
            return result;
        }
        catch (Exception ex)
        {
            _initializationError = $"Model inference failed: {ex.Message}";
            return null;
        }
    }

    private string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Remove common artifacts
        title = title.Trim();
        
        // Remove quotes (including smart quotes)
        title = title.Trim('"', '\'', '\u201C', '\u201D', '\u2018', '\u2019');
        
        // Remove "Title:" prefix if present (case insensitive, with optional space)
        var prefixes = new[] { "Title:", "Title ", "title:", "title " };
        foreach (var prefix in prefixes)
        {
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                title = title[prefix.Length..].Trim();
                break;
            }
        }

        // Remove end tokens if present (do this before word limiting)
        var endTokens = new[] { "<|end|>", "<|user|>", "<|system|>", "<|assistant|>", "\n", "\r" };
        foreach (var endToken in endTokens)
        {
            var idx = title.IndexOf(endToken, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                title = title[..idx].Trim();
        }

        // Remove leading/trailing punctuation that doesn't belong in titles
        title = title.Trim('.', ',', ':', ';', '-', '_', '!', '?');

        // Remove newlines and extra spaces
        title = string.Join(" ", title.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Limit to max 5 words for more descriptive titles
        const int maxWords = 5;
        if (words.Length > maxWords)
            words = words.Take(maxWords).ToArray();

        title = string.Join(" ", words);

        // Title case the result for consistency
        title = ToTitleCase(title);

        // Also enforce character limit for safety (no ellipsis)
        if (title.Length > _settings.MaxTitleLength)
            title = title[.._settings.MaxTitleLength].TrimEnd();

        return title;
    }

    private static string ToTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var words = text.Split(' ');
        var result = new StringBuilder();
        
        // Words that should stay lowercase (unless first word)
        var lowercaseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            "a", "an", "the", "and", "but", "or", "for", "nor", "on", "at", "to", "by", "of", "in" 
        };

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (string.IsNullOrEmpty(word))
                continue;

            if (result.Length > 0)
                result.Append(' ');

            // First word is always capitalized, others follow rules
            if (i == 0 || !lowercaseWords.Contains(word))
            {
                // Capitalize first letter, keep rest as-is (preserves acronyms like API, JSON)
                result.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                    result.Append(word[1..]);
            }
            else
            {
                result.Append(word.ToLowerInvariant());
            }
        }

        return result.ToString();
    }

    private static string PreprocessContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Take the first meaningful portion - focus on the beginning where intent is usually clearest
        var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var meaningfulLines = new List<string>();
        var totalChars = 0;
        const int maxChars = 500; // Focused context for better summarization

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and very short lines (likely formatting)
            if (trimmedLine.Length < 3)
                continue;
                
            // Skip common boilerplate
            if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("#!") || 
                trimmedLine.StartsWith("using ") || trimmedLine.StartsWith("import ") ||
                trimmedLine.StartsWith("namespace ") || trimmedLine.StartsWith("package "))
                continue;

            meaningfulLines.Add(trimmedLine);
            totalChars += trimmedLine.Length;

            if (totalChars >= maxChars)
                break;
        }

        // If we filtered too aggressively, fall back to raw content
        if (meaningfulLines.Count == 0)
        {
            return content.Length > maxChars ? content[..maxChars] : content;
        }

        return string.Join(" ", meaningfulLines);
    }

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)[..16]; // Short hash for cache key
    }

    public void ClearCache()
    {
        lock (_titleCache)
        {
            _titleCache.Clear();
        }
    }

    public void Dispose()
    {
        _tokenizer?.Dispose();
        _model?.Dispose();
        _tokenizer = null;
        _model = null;
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }
}
