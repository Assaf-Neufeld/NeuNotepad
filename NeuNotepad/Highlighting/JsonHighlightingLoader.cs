using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Reflection;
using System.Xml;

namespace NeuNotepad.Highlighting;

public static class JsonHighlightingLoader
{
    private static IHighlightingDefinition? _jsonHighlighting;

    public static IHighlightingDefinition GetJsonHighlighting()
    {
        if (_jsonHighlighting == null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "NeuNotepad.Highlighting.JsonHighlighting.xshd";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                _jsonHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }

        return _jsonHighlighting!;
    }
}
