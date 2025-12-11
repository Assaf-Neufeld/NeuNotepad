using System.IO;
using System.Text.Json;

namespace NeuNotepad.Services;

public class TabState
{
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsModified { get; set; }
    public string EncodingName { get; set; } = "UTF-8";
}

public class SessionState
{
    public List<TabState> Tabs { get; set; } = new();
    public int SelectedTabIndex { get; set; }
}

public static class SessionManager
{
    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeuNotepad",
        "session.json");

    public static void SaveSession(SessionState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(SessionFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(state, options);
            File.WriteAllText(SessionFilePath, json);
        }
        catch
        {
            // Silently fail - session save is not critical
        }
    }

    public static SessionState? LoadSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return null;

            var json = File.ReadAllText(SessionFilePath);
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
            }
        }
        catch
        {
            // Silently fail
        }
    }
}
