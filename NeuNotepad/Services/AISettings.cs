using System.IO;
using System.Text.Json;

namespace NeuNotepad.Services;

/// <summary>
/// Settings for AI-powered features like smart tab titles.
/// </summary>
public class AISettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NeuNotepad",
        "ai-settings.json");

    /// <summary>
    /// Default model path where Phi-3 is typically downloaded.
    /// </summary>
    private static readonly string DefaultModelPath = @"C:\Models\Phi-3-mini\cpu_and_mobile\cpu-int4-rtn-block-32";

    /// <summary>
    /// Whether AI-powered smart titles are enabled.
    /// </summary>
    public bool SmartTitlesEnabled { get; set; }

    /// <summary>
    /// Path to the ONNX model folder (e.g., Phi-3-mini).
    /// </summary>
    public string? ModelPath { get; set; } = DefaultModelPath;

    /// <summary>
    /// Maximum number of characters to send to the model for summarization.
    /// </summary>
    public int MaxContentLength { get; set; } = 4000;

    /// <summary>
    /// Debounce delay in milliseconds before triggering summarization.
    /// </summary>
    public int DebounceDelayMs { get; set; } = 3000;

    /// <summary>
    /// Maximum length of generated title.
    /// </summary>
    public int MaxTitleLength { get; set; } = 50;

    public static AISettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AISettings>(json) ?? new AISettings();
                
                // Ensure ModelPath uses default if null/empty
                if (string.IsNullOrEmpty(settings.ModelPath))
                {
                    settings.ModelPath = DefaultModelPath;
                }
                
                return settings;
            }
        }
        catch
        {
            // Return default settings on error
        }
        return new AISettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail on save error
        }
    }
}
