using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using NeuNotepad.Highlighting;
using NeuNotepad.Models;
using NeuNotepad.Services;
using NeuNotepad.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NeuNotepad;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HashSet<DocumentTab> _markdownPreviewTabs = new();
    private readonly HashSet<TextEditor> _initializedEditors = new();
    private bool _viewModelDisposed;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        TabControl.ItemsSource = _viewModel.Tabs;
        
        _viewModel.StatusChanged += status => StatusText.Text = status;
        _viewModel.EncodingChanged += encoding => EncodingText.Text = encoding;
        _viewModel.TabAdded += tab => TabControl.SelectedItem = tab;
        _viewModel.TabClosed += CleanupClosedTab;
        _viewModel.JsonError += error => MessageBox.Show(error, "JSON Error", 
            MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.MarkdownPreviewToggleRequested += ToggleMarkdownPreview;

        // Initialize and restore session
        Loaded += (s, e) => _viewModel.Initialize();
        
        // Save session on close
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Save session state (including unsaved tabs)
        _viewModel.SaveSession();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        DisposeViewModel();
        Application.Current.Shutdown(0);
    }

    private void DisposeViewModel()
    {
        if (_viewModelDisposed)
        {
            return;
        }

        _viewModel.Dispose();
        _viewModelDisposed = true;
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabControl.SelectedItem is DocumentTab tab)
        {
            _viewModel.CurrentTab = tab;
            UpdateEncodingDisplay(tab);

            // A TabControl hosts the selected ContentTemplate in a shared presenter.
            // Wait until that presenter has switched its DataContext before syncing it.
            Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(TabControl.SelectedItem, tab))
                {
                    SyncActiveTabControls(tab);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void TabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindVisualParent<TabItem>(source) != null || !IsPointInTabHeaderArea(e.GetPosition(TabControl)))
        {
            return;
        }

        if (_viewModel.NewCommand.CanExecute(null))
        {
            _viewModel.NewCommand.Execute(null);
            e.Handled = true;
        }
    }

    private bool IsPointInTabHeaderArea(Point point)
    {
        const double emptyTabStripHeight = 36;

        var firstTab = TabsFirstContainer();
        if (firstTab == null)
        {
            return point.Y <= emptyTabStripHeight;
        }

        var tabBounds = firstTab.TransformToAncestor(TabControl)
            .TransformBounds(new Rect(firstTab.RenderSize));
        return point.Y <= tabBounds.Bottom;
    }

    private TabItem? TabsFirstContainer()
    {
        if (TabControl.Items.Count == 0)
        {
            return null;
        }

        return TabControl.ItemContainerGenerator.ContainerFromIndex(0) as TabItem;
    }

    private void EnableJsonHighlighting_Click(object sender, RoutedEventArgs e)
    {
        ApplyJsonHighlighting();
        StatusText.Text = "JSON highlighting enabled";
    }

    private void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextEditor editor && editor.DataContext is DocumentTab tab)
        {
            if (_initializedEditors.Add(editor))
            {
                // Enable built-in find (Ctrl+F) for each editor instance
                try
                {
                    SearchPanel.Install(editor.TextArea);
                }
                catch (InvalidOperationException)
                {
                    // SearchPanel may already be installed if the editor is reloaded.
                }

                // Track caret position
                editor.TextArea.Caret.PositionChanged += (s, args) =>
                {
                    var line = editor.TextArea.Caret.Line;
                    var column = editor.TextArea.Caret.Column;
                    LineColumnText.Text = $"Ln {line}, Col {column}";
                };

                // The TabControl reuses this editor when selection changes, so resolve
                // the active tab from DataContext instead of capturing the first tab.
                editor.TextChanged += (s, args) =>
                {
                    if (editor.DataContext is not DocumentTab activeTab)
                    {
                        return;
                    }

                    if (IsJsonContent(activeTab.Document.Text) && editor.SyntaxHighlighting == null)
                    {
                        editor.SyntaxHighlighting = JsonHighlightingLoader.GetJsonHighlighting();
                    }

                    if (_markdownPreviewTabs.Contains(activeTab))
                    {
                        var previewViewer = FindMarkdownPreviewViewer(editor);
                        if (previewViewer != null)
                        {
                            previewViewer.Document = MarkdownPreviewRenderer.Render(activeTab.Document.Text);
                        }
                    }

                    _viewModel.OnDocumentTextChanged(activeTab);
                };

                editor.DataContextChanged += (s, args) =>
                {
                    if (args.NewValue is DocumentTab activeTab)
                    {
                        SyncActiveTabControls(activeTab);
                    }
                };
            }

            ApplyHighlighting(editor, tab);
            ApplyMarkdownPreviewState(editor, tab);
            _viewModel.SetCurrentEditor(editor);
        }
    }

    private void MarkdownPreview_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FlowDocumentScrollViewer previewViewer && previewViewer.DataContext is DocumentTab tab)
        {
            var editor = GetCurrentEditor();
            if (editor != null)
            {
                ApplyMarkdownPreviewState(editor, tab);
            }
        }
    }

    private void CleanupClosedTab(DocumentTab tab)
    {
        _markdownPreviewTabs.Remove(tab);
    }

    private void SyncActiveTabControls(DocumentTab tab)
    {
        var editor = GetCurrentEditor();
        if (editor == null)
        {
            return;
        }

        _viewModel.SetCurrentEditor(editor);
        ApplyHighlighting(editor, tab);
        ApplyMarkdownPreviewState(editor, tab);
    }

    private void ApplyHighlighting(TextEditor editor, DocumentTab tab)
    {
        if (tab.IsJsonFile || IsJsonContent(tab.Document.Text))
        {
            editor.SyntaxHighlighting = JsonHighlightingLoader.GetJsonHighlighting();
        }
        else if (IsMarkdownFile(tab.FilePath))
        {
            editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("MarkDown");
        }
        else
        {
            // Check file extension for other highlighting
            var extension = tab.FilePath != null ? Path.GetExtension(tab.FilePath).ToLower() : "";
            editor.SyntaxHighlighting = extension switch
            {
                ".xml" or ".xaml" or ".xshd" => 
                    ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("XML"),
                ".cs" => 
                    ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#"),
                ".js" => 
                    ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("JavaScript"),
                ".html" or ".htm" => 
                    ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("HTML"),
                ".css" => 
                    ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("CSS"),
                _ => null
            };
        }
    }

    private static bool IsJsonContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        // Avoid allocating a trimmed copy of potentially large documents.
        for (int i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (char.IsWhiteSpace(ch))
                continue;

            return ch == '{' || ch == '[';
        }

        return false;
    }

    private static bool IsMarkdownFile(string? filePath)
    {
        var extension = filePath != null ? Path.GetExtension(filePath) : string.Empty;
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateEncodingDisplay(DocumentTab tab)
    {
        var encodingName = tab.Encoding.EncodingName;
        if (tab.Encoding.CodePage == System.Text.Encoding.UTF8.CodePage)
        {
            encodingName = tab.Encoding.GetPreamble().Length > 0 ? "UTF-8 with BOM" : "UTF-8";
        }
        EncodingText.Text = encodingName;
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DocumentTab tab)
        {
            _viewModel.CloseTab(tab);
        }
    }

    public void ApplyJsonHighlighting()
    {
        var editor = GetCurrentEditor();
        if (editor != null)
        {
            editor.SyntaxHighlighting = JsonHighlightingLoader.GetJsonHighlighting();
        }
    }

    private void ToggleMarkdownPreview()
    {
        if (_viewModel.CurrentTab == null)
        {
            StatusText.Text = "Markdown preview: no active tab";
            return;
        }

        TabControl.UpdateLayout();

        var editor = GetCurrentEditor();
        var previewViewer = GetCurrentMarkdownPreviewViewer();
        if (editor == null || previewViewer == null)
        {
            StatusText.Text = $"Markdown preview failed: editor={(editor == null ? "missing" : "ok")}, viewer={(previewViewer == null ? "missing" : "ok")}";
            return;
        }

        if (_markdownPreviewTabs.Contains(_viewModel.CurrentTab))
        {
            _markdownPreviewTabs.Remove(_viewModel.CurrentTab);
            ApplyMarkdownPreviewState(editor, _viewModel.CurrentTab);
            editor.Focus();
            StatusText.Text = $"Markdown preview hidden for {_viewModel.CurrentTab.FileName}";
        }
        else
        {
            _markdownPreviewTabs.Add(_viewModel.CurrentTab);
            previewViewer.Document = MarkdownPreviewRenderer.Render(_viewModel.CurrentTab.Document.Text);
            ApplyMarkdownPreviewState(editor, _viewModel.CurrentTab);
            StatusText.Text = $"Markdown preview shown for {_viewModel.CurrentTab.FileName}";
        }
    }

    private void ToggleMarkdownPreview_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Markdown preview button clicked";
        ToggleMarkdownPreview();
    }

    private void ApplyMarkdownPreviewState(TextEditor editor, DocumentTab tab)
    {
        var previewViewer = FindMarkdownPreviewViewer(editor) ?? GetCurrentMarkdownPreviewViewer();
        if (previewViewer == null)
        {
            StatusText.Text = $"Markdown preview failed: preview viewer not found for {tab.FileName}";
            return;
        }

        if (_markdownPreviewTabs.Contains(tab))
        {
            previewViewer.Document = MarkdownPreviewRenderer.Render(tab.Document.Text);
            previewViewer.Visibility = Visibility.Visible;
            editor.Visibility = Visibility.Collapsed;
        }
        else
        {
            previewViewer.Visibility = Visibility.Collapsed;
            editor.Visibility = Visibility.Visible;
        }
    }

    private TextEditor? GetCurrentEditor()
    {
        return TabControl.SelectedItem == null
            ? null
            : FindVisualChild<TextEditor>(TabControl);
    }

    private FlowDocumentScrollViewer? GetCurrentMarkdownPreviewViewer()
    {
        return TabControl.SelectedItem == null
            ? null
            : FindVisualChild<FlowDocumentScrollViewer>(TabControl);
    }

    private static FlowDocumentScrollViewer? FindMarkdownPreviewViewer(TextEditor editor)
    {
        return editor.Parent is DependencyObject parent
            ? FindVisualChild<FlowDocumentScrollViewer>(parent)
            : null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void GenerateTitle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DocumentTab tab)
        {
            _viewModel.GenerateTitleForTab(tab);
        }
    }
}