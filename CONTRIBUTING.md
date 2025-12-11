# Contributing to NeuNotepad

Thank you for your interest in contributing to NeuNotepad! This document provides guidelines and information for contributors.

## 🚀 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (17.8+) or VS Code with C# extensions
- Git

### Setting Up the Development Environment

1. **Fork the repository** on GitHub

2. **Clone your fork**:
   ```powershell
   git clone https://github.com/YOUR_USERNAME/NeuNotepad.git
   cd NeuNotepad
   ```

3. **Build the project**:
   ```powershell
   dotnet build
   ```

4. **Run the application**:
   ```powershell
   dotnet run --project NeuNotepad
   ```

## 📋 How to Contribute

### Reporting Bugs

Before submitting a bug report:
- Check existing [Issues](https://github.com/YOUR_USERNAME/NeuNotepad/issues) to avoid duplicates
- Make sure you're running the latest version

When submitting a bug report, include:
- A clear, descriptive title
- Steps to reproduce the issue
- Expected vs. actual behavior
- Your environment (OS version, .NET version)
- Screenshots if applicable

### Suggesting Features

Feature requests are welcome! Please:
- Check existing issues to see if it's already been suggested
- Provide a clear use case for the feature
- Explain how it would benefit users

### Submitting Code Changes

1. **Create a feature branch**:
   ```powershell
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** following the code style guidelines below

3. **Test your changes** thoroughly

4. **Commit with a clear message**:
   ```powershell
   git commit -m "Add: brief description of your change"
   ```

5. **Push to your fork**:
   ```powershell
   git push origin feature/your-feature-name
   ```

6. **Open a Pull Request** against the `main` branch

## 🎨 Code Style Guidelines

### General Principles

- Follow existing code patterns and conventions
- Write clear, self-documenting code
- Keep methods small and focused
- Use meaningful variable and method names

### C# Conventions

- Use `PascalCase` for public members and types
- Use `camelCase` for private fields with `_` prefix
- Use `var` when the type is obvious
- Add XML documentation for public APIs

```csharp
/// <summary>
/// Brief description of the method.
/// </summary>
/// <param name="parameter">Description of parameter.</param>
/// <returns>Description of return value.</returns>
public string DoSomething(string parameter)
{
    // Implementation
}
```

### XAML Conventions

- Use consistent indentation (4 spaces)
- Order properties: x:Name, important properties, then alphabetically
- Use data binding over code-behind when possible

### MVVM Pattern

This project follows the MVVM pattern:
- **Models**: Data structures in `Models/`
- **ViewModels**: Application logic in `ViewModels/`
- **Views**: XAML UI in root folder
- **Commands**: Use `RelayCommand` from `Commands/`

## 📁 Project Structure

```
NeuNotepad/
├── Commands/        # ICommand implementations
├── Converters/      # Value converters for XAML binding
├── Highlighting/    # Syntax highlighting definitions
├── Models/          # Data models
├── Services/        # Business logic and utilities
├── ViewModels/      # MVVM ViewModels
├── Assets/          # Icons and resources
├── MainWindow.xaml  # Main window UI
└── App.xaml         # Application entry
```

### Adding New Features

#### New Commands
1. Define the command in `MainViewModel.cs`
2. Add keyboard binding in `MainWindow.xaml` under `Window.InputBindings`
3. Add menu item if needed

#### New Syntax Highlighting
1. Create a `.xshd` file in `Highlighting/`
2. Add it as `EmbeddedResource` in `.csproj`
3. Create a loader similar to `JsonHighlightingLoader.cs`

#### New Services
1. Create service class in `Services/`
2. Inject into ViewModel constructor
3. Follow single responsibility principle

## ✅ Pull Request Checklist

Before submitting a PR, ensure:

- [ ] Code compiles without errors
- [ ] No new warnings introduced
- [ ] Feature works as intended
- [ ] Code follows existing style
- [ ] Commit messages are clear
- [ ] PR description explains the changes

## 📜 License

By contributing, you agree that your contributions will be licensed under the MIT License.

## 💬 Questions?

If you have questions, feel free to:
- Open an issue with the `question` label
- Start a discussion in the Discussions tab

Thank you for contributing! 🎉
