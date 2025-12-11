using System.Text.Json;

namespace NeuNotepad.Services;

public class JsonFormatResult
{
    public bool Success { get; set; }
    public string? FormattedJson { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorLine { get; set; }
}

public static class JsonFormatter
{
    public static JsonFormatResult Format(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = "Input is empty"
            };
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var formattedJson = JsonSerializer.Serialize(document.RootElement, options);
            
            return new JsonFormatResult
            {
                Success = true,
                FormattedJson = formattedJson
            };
        }
        catch (JsonException ex)
        {
            return new JsonFormatResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorLine = (int?)ex.LineNumber + 1 // LineNumber is 0-based
            };
        }
    }

    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
