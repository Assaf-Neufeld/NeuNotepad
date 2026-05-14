using System.Text.Json;
using System.Text.RegularExpressions;

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
    private static readonly JsonDocumentOptions LenientDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Regex UnquotedPropertyNamePattern = new(
        @"(^|[\{,]\s*)([A-Za-z_$][A-Za-z0-9_$.-]*)(\s*:)",
        RegexOptions.Compiled | RegexOptions.Multiline);

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
            using var document = ParseLenient(json);
            var formattedJson = JsonSerializer.Serialize(document.RootElement, SerializerOptions);
            
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
            using var document = ParseLenient(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static JsonDocument ParseLenient(string json)
    {
        var trimmedJson = json.Trim().TrimStart('\uFEFF');

        try
        {
            return JsonDocument.Parse(trimmedJson, LenientDocumentOptions);
        }
        catch (JsonException)
        {
            var normalizedJson = NormalizeJsonLikeText(trimmedJson);
            return JsonDocument.Parse(normalizedJson, LenientDocumentOptions);
        }
    }

    private static string NormalizeJsonLikeText(string json)
    {
        var normalized = json
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u201E', '"')
            .Replace('\u201F', '"')
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'');

        normalized = ConvertSingleQuotedStrings(normalized);
        normalized = UnquotedPropertyNamePattern.Replace(normalized, "$1\"$2\"$3");

        return normalized;
    }

    private static string ConvertSingleQuotedStrings(string json)
    {
        var result = new System.Text.StringBuilder(json.Length);
        var inDoubleQuotedString = false;
        var inSingleQuotedString = false;

        for (var index = 0; index < json.Length; index++)
        {
            var character = json[index];

            if (character == '\\' && inSingleQuotedString)
            {
                if (index + 1 >= json.Length)
                {
                    result.Append(character);
                    continue;
                }

                var escapedCharacter = json[++index];
                result.Append(escapedCharacter switch
                {
                    '\'' => "'",
                    '"' => "\\\"",
                    _ => $"\\{escapedCharacter}"
                });
                continue;
            }

            if (character == '\\' && inDoubleQuotedString)
            {
                result.Append(character);
                if (index + 1 < json.Length)
                {
                    result.Append(json[++index]);
                }

                continue;
            }

            if (character == '"' && !inSingleQuotedString)
            {
                inDoubleQuotedString = !inDoubleQuotedString;
                result.Append(character);
                continue;
            }

            if (character == '\'' && !inDoubleQuotedString)
            {
                inSingleQuotedString = !inSingleQuotedString;
                result.Append('"');
                continue;
            }

            result.Append(character);
        }

        return result.ToString();
    }
}
