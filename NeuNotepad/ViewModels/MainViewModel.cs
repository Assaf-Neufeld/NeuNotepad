using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using NeuNotepad.Commands;
using NeuNotepad.Models;
using NeuNotepad.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace NeuNotepad.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private DocumentTab? _currentTab;
    private TextEditor? _currentEditor;
    private int _tabCounter = 0;
    
    // Regex to extract number from "Untitled X" filenames
    private static readonly System.Text.RegularExpressions.Regex UntitledPattern = 
        new(@"^Untitled\s+(\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);
    
    // AI Title Generation
    private readonly AISettings _aiSettings;
    private readonly TitleSummarizationService _titleService;
    private readonly Dictionary<DocumentTab, DispatcherTimer> _debounceTimers = new();
    private readonly Dictionary<DocumentTab, CancellationTokenSource> _pendingGenerations = new();

    public MainViewModel()
    {
        Tabs = new ObservableCollection<DocumentTab>();
        
        // Initialize AI services
        _aiSettings = AISettings.Load();
        _titleService = new TitleSummarizationService(_aiSettings);
        _titleService.InitializationStatusChanged += OnTitleServiceStatusChanged;
        
        NewCommand = new RelayCommand(NewFile);
        OpenCommand = new RelayCommand(OpenFile);
        SaveCommand = new RelayCommand(SaveFile, () => CurrentTab != null);
        SaveAsCommand = new RelayCommand(SaveFileAs, () => CurrentTab != null);
        CloseTabCommand = new RelayCommand(CloseCurrentTab, () => CurrentTab != null);
        CloseAllTabsCommand = new RelayCommand(CloseAllTabs, () => Tabs.Count > 0);
        ExitCommand = new RelayCommand(RequestExit);
        
        UndoCommand = new RelayCommand(Undo, () => _currentEditor?.CanUndo ?? false);
        RedoCommand = new RelayCommand(Redo, () => _currentEditor?.CanRedo ?? false);
        CutCommand = new RelayCommand(Cut, () => _currentEditor != null);
        CopyCommand = new RelayCommand(Copy, () => _currentEditor != null);
        PasteCommand = new RelayCommand(Paste, () => _currentEditor != null);
        SelectAllCommand = new RelayCommand(SelectAll, () => _currentEditor != null);
        
        FormatJsonCommand = new RelayCommand(FormatJson, () => CurrentTab != null);
        ToggleMarkdownPreviewCommand = new RelayCommand(ToggleMarkdownPreview);
        
        // AI Commands
        ConfigureAICommand = new RelayCommand(ConfigureAI);
        ToggleSmartTitlesCommand = new RelayCommand(ToggleSmartTitles);
        GenerateTitleNowCommand = new RelayCommand(GenerateTitleNow, () => CurrentTab != null);
    }

    private void OnTitleServiceStatusChanged(bool success, string? error)
    {
        if (success)
        {
            StatusChanged?.Invoke("AI model loaded successfully");
        }
        else if (!string.IsNullOrEmpty(error))
        {
            StatusChanged?.Invoke($"AI: {error}");
        }
    }

    public void Initialize()
    {
        // Try to restore previous session
        var session = SessionManager.LoadSession();
        if (session != null && session.Tabs.Count > 0)
        {
            RestoreSession(session);
            UpdateTabCounterFromExistingTabs();
        }
        else
        {
            // Create initial tab if no session
            NewFile();
        }

        StatusChanged?.Invoke(_aiSettings.SmartTitlesEnabled
            ? "Smart titles enabled; model loads on first title generation"
            : "Smart titles disabled");
    }

    /// <summary>
    /// Updates _tabCounter to be higher than any existing "Untitled X" tab numbers.
    /// This prevents duplicate names when creating new tabs after restoring a session.
    /// </summary>
    private void UpdateTabCounterFromExistingTabs()
    {
        int maxNumber = 0;
        foreach (var tab in Tabs)
        {
            var match = UntitledPattern.Match(tab.FileName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            {
                maxNumber = Math.Max(maxNumber, number);
            }
        }
        _tabCounter = maxNumber;
    }

    private void RestoreSession(SessionState session)
    {
        foreach (var tabState in session.Tabs)
        {
            var tab = new DocumentTab
            {
                FileName = tabState.FileName ?? $"Untitled {++_tabCounter}",
                FilePath = tabState.FilePath,
                Encoding = GetEncodingFromName(tabState.EncodingName)
            };
            tab.Document.Text = tabState.Content;
            tab.IsModified = tabState.IsModified;

            Tabs.Add(tab);
        }

        if (Tabs.Count > 0)
        {
            var index = Math.Min(session.SelectedTabIndex, Tabs.Count - 1);
            CurrentTab = Tabs[Math.Max(0, index)];
            TabAdded?.Invoke(CurrentTab);
        }

        StatusChanged?.Invoke($"Restored {Tabs.Count} tab(s) from previous session");
    }

    public void SaveSession()
    {
        var session = new SessionState
        {
            SelectedTabIndex = CurrentTab != null ? Tabs.IndexOf(CurrentTab) : 0,
            Tabs = Tabs.Select(tab => new TabState
            {
                FilePath = tab.FilePath,
                FileName = tab.FileName,
                Content = tab.Document.Text,
                IsModified = tab.IsModified,
                EncodingName = tab.Encoding.WebName
            }).ToList()
        };

        SessionManager.SaveSession(session);
    }

    private static Encoding GetEncodingFromName(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private void RequestExit()
    {
        SaveSession();
        Application.Current.Shutdown();
    }

    public ObservableCollection<DocumentTab> Tabs { get; }

    public DocumentTab? CurrentTab
    {
        get => _currentTab;
        set
        {
            _currentTab = value;
            OnPropertyChanged();
        }
    }

    public void SetCurrentEditor(TextEditor? editor)
    {
        _currentEditor = editor;
    }

    // Commands
    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand CloseAllTabsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand FormatJsonCommand { get; }
    public ICommand ToggleMarkdownPreviewCommand { get; }
    
    // AI Commands
    public ICommand ConfigureAICommand { get; }
    public ICommand ToggleSmartTitlesCommand { get; }
    public ICommand GenerateTitleNowCommand { get; }
    
    // AI Settings
    public bool SmartTitlesEnabled => _aiSettings.SmartTitlesEnabled;
    public string? ModelPath => _aiSettings.ModelPath;

    // Events for UI updates
    public event Action<string>? StatusChanged;
    public event Action<string>? EncodingChanged;
    public event Action<DocumentTab>? TabAdded;
    public event Action<DocumentTab>? TabClosed;
    public event Action<string>? JsonError;
    public event Action? MarkdownPreviewToggleRequested;

    private void NewFile()
    {
        var tab = new DocumentTab
        {
            FileName = $"Untitled {++_tabCounter}"
        };
        Tabs.Add(tab);
        CurrentTab = tab;
        TabAdded?.Invoke(tab);
        StatusChanged?.Invoke("New file created");
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|Markdown Files (*.md;*.markdown)|*.md;*.markdown",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            OpenFileFromPath(dialog.FileName);
        }
    }

    public void OpenFileFromPath(string filePath)
    {
        try
        {
            // Check if file is already open
            var existingTab = Tabs.FirstOrDefault(t => 
                string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            
            if (existingTab != null)
            {
                CurrentTab = existingTab;
                return;
            }

            var encoding = EncodingDetector.DetectEncoding(filePath);

            var tab = new DocumentTab
            {
                FilePath = filePath,
                Encoding = encoding
            };

            LoadFileIntoDocument(tab.Document, filePath, encoding);
            tab.IsModified = false;

            Tabs.Add(tab);
            CurrentTab = tab;
            TabAdded?.Invoke(tab);
            
            EncodingChanged?.Invoke(GetEncodingName(encoding));
            StatusChanged?.Invoke($"Opened: {filePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening file: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void LoadFileIntoDocument(ICSharpCode.AvalonEdit.Document.TextDocument document, string filePath, Encoding encoding)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 128 * 1024,
            options: FileOptions.SequentialScan);

        using var reader = new StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 128 * 1024,
            leaveOpen: false);

        var undo = document.UndoStack;
        var previousSizeLimit = undo.SizeLimit;
        undo.SizeLimit = 0;

        document.BeginUpdate();
        try
        {
            if (document.TextLength > 0)
            {
                document.Remove(0, document.TextLength);
            }

            char[] charBuffer = ArrayPool<char>.Shared.Rent(128 * 1024);
            try
            {
                while (true)
                {
                    var charsRead = reader.Read(charBuffer, 0, charBuffer.Length);
                    if (charsRead <= 0)
                        break;

                    document.Insert(document.TextLength, new string(charBuffer, 0, charsRead));
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(charBuffer);
            }
        }
        finally
        {
            document.EndUpdate();
            undo.ClearAll();
            undo.SizeLimit = previousSizeLimit;
        }
    }

    private void SaveFile()
    {
        if (CurrentTab == null) return;

        if (string.IsNullOrEmpty(CurrentTab.FilePath))
        {
            SaveFileAs();
        }
        else
        {
            SaveToFile(CurrentTab.FilePath);
        }
    }

    private void SaveFileAs()
    {
        if (CurrentTab == null) return;

        // Use SmartTitle as default filename if available, otherwise use current filename
        var suggestedName = !string.IsNullOrEmpty(CurrentTab.SmartTitle) && !CurrentTab.HasBeenSaved
            ? SanitizeFileName(CurrentTab.SmartTitle) + ".txt"
            : CurrentTab.FileName;

        var dialog = new SaveFileDialog
        {
            Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|Markdown Files (*.md;*.markdown)|*.md;*.markdown",
            FilterIndex = 1,
            FileName = suggestedName
        };

        if (dialog.ShowDialog() == true)
        {
            SaveToFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Removes invalid filename characters from a string.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Where(c => !invalidChars.Contains(c)));
        return string.IsNullOrWhiteSpace(sanitized) ? "Untitled" : sanitized.Trim();
    }

    private void SaveToFile(string filePath)
    {
        if (CurrentTab == null) return;

        try
        {
            File.WriteAllText(filePath, CurrentTab.Document.Text, CurrentTab.Encoding);
            CurrentTab.FilePath = filePath;
            CurrentTab.HasBeenSaved = true;
            CurrentTab.IsModified = false;
            StatusChanged?.Invoke($"Saved: {filePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseCurrentTab()
    {
        if (CurrentTab == null) return;
        CloseTab(CurrentTab);
    }

    public bool CloseTab(DocumentTab tab)
    {
        if (tab.IsModified)
        {
            var result = MessageBox.Show(
                $"Do you want to save changes to {tab.FileName}?",
                "Save Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    CurrentTab = tab;
                    SaveFile();
                    if (tab.IsModified) return false; // Save was cancelled
                    break;
                case MessageBoxResult.Cancel:
                    return false;
            }
        }

        CleanupTabResources(tab);

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        TabClosed?.Invoke(tab);

        if (Tabs.Count > 0)
        {
            CurrentTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        }
        else
        {
            CurrentTab = null;
        }

        return true;
    }

    private void CloseAllTabs()
    {
        while (Tabs.Count > 0)
        {
            if (!CloseTab(Tabs[0]))
                break;
        }
    }

    private void Undo()
    {
        _currentEditor?.Undo();
    }

    private void Redo()
    {
        _currentEditor?.Redo();
    }

    private void Cut()
    {
        _currentEditor?.Cut();
    }

    private void Copy()
    {
        _currentEditor?.Copy();
    }

    private void Paste()
    {
        _currentEditor?.Paste();
    }

    private void SelectAll()
    {
        _currentEditor?.SelectAll();
    }

    private void FormatJson()
    {
        if (CurrentTab == null || _currentEditor == null) return;

        var result = JsonFormatter.Format(CurrentTab.Document.Text);

        if (result.Success && result.FormattedJson != null)
        {
            CurrentTab.Document.Text = result.FormattedJson;
            StatusChanged?.Invoke("JSON formatted successfully");
        }
        else
        {
            var errorMsg = result.ErrorLine.HasValue
                ? $"Invalid JSON at line {result.ErrorLine}: {result.ErrorMessage}"
                : $"Invalid JSON: {result.ErrorMessage}";
            
            JsonError?.Invoke(errorMsg);
            StatusChanged?.Invoke("JSON formatting failed");
        }
    }

    private void ToggleMarkdownPreview()
    {
        if (CurrentTab == null)
        {
            StatusChanged?.Invoke("Markdown preview: no active tab");
            return;
        }

        StatusChanged?.Invoke($"Markdown preview requested for {CurrentTab.FileName}");
        MarkdownPreviewToggleRequested?.Invoke();
    }

    private static string GetEncodingName(Encoding encoding)
    {
        if (encoding.CodePage == Encoding.UTF8.CodePage)
            return encoding.GetPreamble().Length > 0 ? "UTF-8 with BOM" : "UTF-8";
        if (encoding.CodePage == Encoding.Unicode.CodePage)
            return "UTF-16 LE";
        if (encoding.CodePage == Encoding.BigEndianUnicode.CodePage)
            return "UTF-16 BE";
        if (encoding.CodePage == Encoding.UTF32.CodePage)
            return "UTF-32";
        
        return encoding.EncodingName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region AI Title Generation

    /// <summary>
    /// Hook this to the document's TextChanged event to enable debounced title generation.
    /// </summary>
    public void OnDocumentTextChanged(DocumentTab tab)
    {
        if (!_aiSettings.SmartTitlesEnabled || string.IsNullOrEmpty(_aiSettings.ModelPath))
            return;

        // Don't generate titles for saved files - they should keep their filename
        if (tab.HasBeenSaved || !string.IsNullOrEmpty(tab.FilePath))
            return;

        // Cancel any pending generation for this tab
        if (_pendingGenerations.TryGetValue(tab, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
            _pendingGenerations.Remove(tab);
        }

        // Reset or create debounce timer
        if (!_debounceTimers.TryGetValue(tab, out var timer))
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_aiSettings.DebounceDelayMs)
            };
            timer.Tick += (s, e) => OnDebounceTimerTick(tab, timer);
            _debounceTimers[tab] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private async void OnDebounceTimerTick(DocumentTab tab, DispatcherTimer timer)
    {
        timer.Stop();

        if (!Tabs.Contains(tab))
        {
            CleanupTabResources(tab);
            return;
        }

        await GenerateTitleForTabAsync(tab);
    }

    private async Task GenerateTitleForTabAsync(DocumentTab tab)
    {
        // Check if AI is configured
        if (string.IsNullOrEmpty(_aiSettings.ModelPath))
        {
            StatusChanged?.Invoke("AI model not configured. Use AI → Configure Model Path.");
            return;
        }

        if (!Directory.Exists(_aiSettings.ModelPath))
        {
            StatusChanged?.Invoke($"AI model path not found: {_aiSettings.ModelPath}");
            return;
        }

        // Don't generate for saved files
        if (tab.HasBeenSaved || !string.IsNullOrEmpty(tab.FilePath))
        {
            StatusChanged?.Invoke("Smart titles disabled for saved files");
            return;
        }

        var content = tab.Document.Text;
        if (string.IsNullOrWhiteSpace(content) || content.Length < 20)
        {
            // Too short to summarize meaningfully
            tab.SmartTitle = null;
            StatusChanged?.Invoke("Content too short for smart title (need 20+ chars)");
            return;
        }

        var cts = new CancellationTokenSource();
        _pendingGenerations[tab] = cts;

        try
        {
            tab.IsGeneratingTitle = true;
            StatusChanged?.Invoke("Generating smart title...");
            var title = await _titleService.GenerateTitleAsync(content, cts.Token);
            
            if (!cts.Token.IsCancellationRequested && Tabs.Contains(tab))
            {
                tab.SmartTitle = title;
                if (!string.IsNullOrEmpty(title))
                {
                    StatusChanged?.Invoke($"Smart title: {title}");
                }
                else
                {
                    var error = _titleService.InitializationError;
                    StatusChanged?.Invoke($"Could not generate title: {error ?? "unknown error"}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled, ignore
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Title generation error: {ex.Message}");
        }
        finally
        {
            tab.IsGeneratingTitle = false;
            if (_pendingGenerations.TryGetValue(tab, out var storedCts) && storedCts == cts)
            {
                _pendingGenerations.Remove(tab);
            }
            cts.Dispose();
        }
    }

    private void ConfigureAI()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Phi-3 Model Folder (select any file inside the model folder)",
            Filter = "All Files (*.*)|*.*",
            CheckFileExists = true
        };

        // Use a FolderBrowserDialog-style approach by selecting a file in the model folder
        if (dialog.ShowDialog() == true)
        {
            var modelFolder = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(modelFolder))
            {
                // Verify it looks like a model folder (should contain genai_config.json or similar)
                var configFile = Path.Combine(modelFolder, "genai_config.json");
                if (File.Exists(configFile))
                {
                    _aiSettings.ModelPath = modelFolder;
                    _aiSettings.Save();
                    OnPropertyChanged(nameof(ModelPath));
                    StatusChanged?.Invoke($"AI model path set to: {modelFolder}");
                }
                else
                {
                    MessageBox.Show(
                        "The selected folder doesn't appear to contain a valid ONNX GenAI model.\n\n" +
                        "Please download Phi-3-mini-4k-instruct-onnx from Hugging Face and select a file inside that folder.",
                        "Invalid Model Folder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
    }

    private void ToggleSmartTitles()
    {
        _aiSettings.SmartTitlesEnabled = !_aiSettings.SmartTitlesEnabled;
        _aiSettings.Save();
        OnPropertyChanged(nameof(SmartTitlesEnabled));
        
        if (_aiSettings.SmartTitlesEnabled)
        {
            StatusChanged?.Invoke(!string.IsNullOrEmpty(_aiSettings.ModelPath)
                ? "Smart Titles enabled; model loads on first title generation"
                : "Smart Titles enabled - configure model path first");
        }
        else
        {
            StatusChanged?.Invoke("Smart Titles disabled");
            // Clear all smart titles
            foreach (var tab in Tabs)
            {
                tab.SmartTitle = null;
            }
        }
    }

    private void GenerateTitleNow()
    {
        if (CurrentTab == null) return;
        GenerateTitleForTab(CurrentTab);
    }

    /// <summary>
    /// Public method to generate title for a specific tab (called from UI button).
    /// </summary>
    public void GenerateTitleForTab(DocumentTab tab)
    {
        if (!_aiSettings.SmartTitlesEnabled)
        {
            StatusChanged?.Invoke("Enable Smart Titles first (AI menu)");
            return;
        }

        if (string.IsNullOrEmpty(_aiSettings.ModelPath))
        {
            StatusChanged?.Invoke("Configure AI model path first (AI menu)");
            return;
        }

        _ = GenerateTitleForTabAsync(tab);
    }

    private void CleanupTabResources(DocumentTab tab)
    {
        if (_debounceTimers.TryGetValue(tab, out var timer))
        {
            timer.Stop();
            _debounceTimers.Remove(tab);
        }

        if (_pendingGenerations.TryGetValue(tab, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _pendingGenerations.Remove(tab);
        }
    }

    #endregion

    public void Dispose()
    {
        foreach (var timer in _debounceTimers.Values)
        {
            timer.Stop();
        }
        _debounceTimers.Clear();

        foreach (var cts in _pendingGenerations.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _pendingGenerations.Clear();

        _titleService.Dispose();
        GC.SuppressFinalize(this);
    }
}
