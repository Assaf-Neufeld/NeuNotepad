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
            // Craft prompt for title generation - simple and direct
            var prompt = $"""
                <|user|>
                What is the main topic of this text? Reply with exactly 3 or 4 words only. No punctuation:
                {content}
                <|end|>
                <|assistant|>
                """;

            var sequences = _tokenizer.Encode(prompt);

            using var generatorParams = new GeneratorParams(_model);
            generatorParams.SetSearchOption("max_length", 512); // Input + output length (support larger content)
            generatorParams.SetSearchOption("temperature", 0.3); // Low temperature for consistent output
            generatorParams.SetInputSequences(sequences);

            var outputTokens = new List<int>();
            using var generator = new Generator(_model, generatorParams);
            
            while (!generator.IsDone())
            {
                generator.ComputeLogits();
                generator.GenerateNextToken();
                
                var token = generator.GetSequence(0)[^1];
                outputTokens.Add(token);
                
                // Stop if we've generated enough
                if (outputTokens.Count > 20)
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
        
        // Remove "Title:" prefix if present
        if (title.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
            title = title[6..].Trim();

        // Remove end tokens if present (do this before word limiting)
        var endTokens = new[] { "<|end|>", "<|user|>", "<|system|>", "<|assistant|>" };
        foreach (var endToken in endTokens)
        {
            var idx = title.IndexOf(endToken, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                title = title[..idx].Trim();
        }

        // Remove newlines and extra spaces
        title = string.Join(" ", title.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Limit to max 4 words (no ellipsis - clean filename)
        const int maxWords = 4;
        if (words.Length > maxWords)
            words = words.Take(maxWords).ToArray();

        title = string.Join(" ", words);

        // Also enforce character limit for safety (no ellipsis)
        if (title.Length > _settings.MaxTitleLength)
            title = title[.._settings.MaxTitleLength].TrimEnd();

        return title;
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
