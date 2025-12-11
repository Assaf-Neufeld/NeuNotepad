# NeuNotepad

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A lightweight, modern desktop notepad application for Windows with first-class JSON support and AI-powered smart features.

![NeuNotepad Screenshot](docs/screenshot.png)

## ✨ Features

### 📝 Core Text Editing
- Free typing and editing with a modern interface
- Full clipboard support (Cut, Copy, Paste)
- Unlimited Undo / Redo
- Standard keyboard shortcuts

### 📁 File Operations
- Open text files from disk with **automatic encoding detection**
- Save and Save As functionality
- Supports UTF-8, UTF-16, UTF-32, and other encodings
- Unsaved changes indicator (*) in tab header
- **Session persistence** - your tabs are restored on restart

### 📑 Multi-Tab Support
- Work with multiple files simultaneously
- Each tab maintains its own editor state
- Close individual tabs or all tabs at once
- Tab state persisted across sessions

### 🔧 JSON Support
- **Format JSON**: Pretty-print JSON with 2-space indentation (`Ctrl+Shift+F`)
- **Syntax Highlighting** for:
  - Property keys (blue, bold)
  - Strings (red)
  - Numbers (green)
  - Booleans (`true`/`false`) (blue, bold)
  - `null` values (blue, bold)
  - Brackets and punctuation
- Invalid JSON detection with line number reporting

### 🤖 AI-Powered Features (Optional)
- **Smart Tab Titles**: Automatically generate descriptive tab names from document content
- Uses local Phi-3 Mini model via ONNX Runtime (runs entirely offline)
- Configurable model path and settings
- Toggle on/off via Edit menu

## ⌨️ Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| New File | `Ctrl+N` |
| Open File | `Ctrl+O` |
| Save | `Ctrl+S` |
| Save As | `Ctrl+Shift+S` |
| Format JSON | `Ctrl+Shift+F` |
| Undo | `Ctrl+Z` |
| Redo | `Ctrl+Y` |
| Close Tab | `Ctrl+W` |
| Cut | `Ctrl+X` |
| Copy | `Ctrl+C` |
| Paste | `Ctrl+V` |
| Select All | `Ctrl+A` |

## 📋 Requirements

- **OS**: Windows 10 (version 1903 or later) or Windows 11
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Optional (for AI features)
- [Phi-3 Mini ONNX model](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx) downloaded locally
- ~2GB disk space for the model files

## 🚀 Installation

### Option 1: Download Release
Download the latest release from the [Releases](https://github.com/YOUR_USERNAME/NeuNotepad/releases) page.

### Option 2: Build from Source

```powershell
# Clone the repository
git clone https://github.com/YOUR_USERNAME/NeuNotepad.git
cd NeuNotepad

# Build the project
dotnet build

# Run the application
dotnet run --project NeuNotepad
```

Or run the built executable directly:
```powershell
.\NeuNotepad\bin\Debug\net8.0-windows\NeuNotepad.exe
```

## 🔧 Configuration

### AI Settings (Optional)

To enable AI-powered smart titles:

1. Download the [Phi-3 Mini ONNX model](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx)
2. Extract to a local folder (e.g., `C:\Models\Phi-3-mini\cpu_and_mobile\cpu-int4-rtn-block-32`)
3. Configure the model path in **Edit > Configure AI...**

Settings are stored in `%APPDATA%\NeuNotepad\ai-settings.json`.

## 🛠️ Technologies

| Technology | Purpose |
|------------|---------|
| [.NET 8.0](https://dotnet.microsoft.com/) | Application framework |
| [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) | Windows UI framework |
| [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) | Advanced text editor control |
| [ONNX Runtime GenAI](https://github.com/microsoft/onnxruntime-genai) | Local AI inference |

## 📁 Project Structure

```
NeuNotepad/
├── Commands/
│   └── RelayCommand.cs           # ICommand implementation for MVVM
├── Converters/
│   └── InverseBoolToVisibilityConverter.cs
├── Highlighting/
│   ├── JsonHighlighting.xshd     # JSON syntax highlighting rules
│   └── JsonHighlightingLoader.cs
├── Models/
│   └── DocumentTab.cs            # Tab/document model
├── Services/
│   ├── AISettings.cs             # AI feature configuration
│   ├── EncodingDetector.cs       # Automatic encoding detection
│   ├── JsonFormatter.cs          # JSON formatting/validation
│   ├── SessionState.cs           # Session persistence
│   └── TitleSummarizationService.cs  # AI title generation
├── ViewModels/
│   └── MainViewModel.cs          # Main application logic (MVVM)
├── MainWindow.xaml               # Main window UI
├── MainWindow.xaml.cs            # Code-behind
└── App.xaml                      # Application entry point
```

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) for the excellent text editor control
- [Microsoft Phi-3](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx) for the local AI model
- The .NET and WPF community for excellent documentation and resources

For personal use.
