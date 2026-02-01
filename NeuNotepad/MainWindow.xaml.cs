using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;
using NeuNotepad.Highlighting;
using NeuNotepad.Models;
using NeuNotepad.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NeuNotepad;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        TabControl.ItemsSource = _viewModel.Tabs;
        
        _viewModel.StatusChanged += status => StatusText.Text = status;
        _viewModel.EncodingChanged += encoding => EncodingText.Text = encoding;
        _viewModel.TabAdded += tab => TabControl.SelectedItem = tab;
        _viewModel.JsonError += error => MessageBox.Show(error, "JSON Error", 
            MessageBoxButton.OK, MessageBoxImage.Warning);

        // Initialize and restore session
        Loaded += (s, e) => _viewModel.Initialize();
        
        // Save session on close
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Save session state (including unsaved tabs)
        _viewModel.SaveSession();
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabControl.SelectedItem is DocumentTab tab)
        {
            _viewModel.CurrentTab = tab;
            UpdateEncodingDisplay(tab);
            
            // Update current editor reference when tab changes
            var editor = GetCurrentEditor();
            if (editor != null)
            {
                _viewModel.SetCurrentEditor(editor);
            }
        }
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
            // Enable built-in find (Ctrl+F) for each editor instance
            try
            {
                SearchPanel.Install(editor.TextArea);
            }
            catch (InvalidOperationException)
            {
                // SearchPanel may already be installed if the editor is reloaded.
            }

            // Apply JSON highlighting if it's a JSON file
            ApplyHighlighting(editor, tab);
            
            // Track caret position
            editor.TextArea.Caret.PositionChanged += (s, args) =>
            {
                var line = editor.TextArea.Caret.Line;
                var column = editor.TextArea.Caret.Column;
                LineColumnText.Text = $"Ln {line}, Col {column}";
            };

            // Set the current editor in the view model
            _viewModel.SetCurrentEditor(editor);

            // Auto-detect JSON content when text changes
            bool lastWasJson = false;
            editor.TextChanged += (s, args) =>
            {
                var isJson = IsJsonContent(tab.Document.Text);
                if (isJson != lastWasJson)
                {
                    lastWasJson = isJson;
                    if (isJson && editor.SyntaxHighlighting == null)
                    {
                        editor.SyntaxHighlighting = JsonHighlightingLoader.GetJsonHighlighting();
                    }
                }
            };

            // Monitor for file extension changes (for highlighting)
            tab.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(DocumentTab.FilePath))
                {
                    ApplyHighlighting(editor, tab);
                }
            };
        }
    }

    private void ApplyHighlighting(TextEditor editor, DocumentTab tab)
    {
        if (tab.IsJsonFile || IsJsonContent(tab.Document.Text))
        {
            editor.SyntaxHighlighting = JsonHighlightingLoader.GetJsonHighlighting();
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

    private TextEditor? GetCurrentEditor()
    {
        if (TabControl.SelectedItem == null) return null;
        
        var container = TabControl.ItemContainerGenerator.ContainerFromItem(TabControl.SelectedItem) as TabItem;
        if (container == null) return null;
        
        var contentPresenter = FindVisualChild<ContentPresenter>(container);
        if (contentPresenter == null) return null;
        
        var template = contentPresenter.ContentTemplate;
        return template?.FindName("Editor", contentPresenter) as TextEditor;
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

    private void GenerateTitle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DocumentTab tab)
        {
            _viewModel.GenerateTitleForTab(tab);
        }
    }

    private void TabControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Check if the double-click was on the empty area (not on a TabItem)
        // We need to check if the original source is the TabControl or its panel,
        // and not a TabItem or any of its child elements
        var originalSource = e.OriginalSource as DependencyObject;
        
        // Walk up the visual tree to see if we hit a TabItem
        while (originalSource != null)
        {
            if (originalSource is TabItem)
            {
                // Double-click was on a tab item, not the empty area
                return;
            }
            
            if (originalSource == TabControl)
            {
                // Reached the TabControl without hitting a TabItem - this is the empty area
                break;
            }
            
            originalSource = VisualTreeHelper.GetParent(originalSource);
        }
        
        // If we're here, it's the empty area - create a new tab
        _viewModel.NewCommand?.Execute(null);
        e.Handled = true;
    }
}