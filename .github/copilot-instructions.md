# NeuNotepad Development Guidelines

## Project Overview
NeuNotepad is a lightweight WPF notepad application with JSON support, built on .NET 8.

## Technology Stack
- **Framework**: .NET 8.0 (LTS)
- **UI**: WPF (Windows Presentation Foundation)
- **Text Editor**: AvalonEdit for advanced text editing and syntax highlighting
- **Architecture**: MVVM pattern

## Building and Running

### Build
```powershell
cd NeuNotepad
dotnet build
```

### Run
```powershell
dotnet run --project NeuNotepad
```

## Code Organization

- **Commands/**: ICommand implementations for MVVM binding
- **Highlighting/**: Syntax highlighting definitions (XSHD format)
- **Models/**: Data models (DocumentTab for tab state)
- **Services/**: Utility services (encoding detection, JSON formatting)
- **ViewModels/**: MVVM ViewModels

## Key Patterns

### Adding New Commands
1. Define the command in `MainViewModel.cs`
2. Add keyboard binding in `MainWindow.xaml` under `Window.InputBindings`
3. Add menu item if needed

### Adding New Syntax Highlighting
1. Create a `.xshd` file in `Highlighting/` folder
2. Add it as `EmbeddedResource` in the `.csproj`
3. Create a loader similar to `JsonHighlightingLoader.cs`

## Dependencies
- **AvalonEdit**: NuGet package for advanced text editing
