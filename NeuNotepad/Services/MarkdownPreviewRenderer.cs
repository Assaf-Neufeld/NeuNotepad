using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace NeuNotepad.Services;

public static class MarkdownPreviewRenderer
{
    private static readonly FontFamily PreviewFont = new("Segoe UI");
    private static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas");
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(24, 34, 47));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(91, 102, 115));
    private static readonly Brush CodeBackgroundBrush = new SolidColorBrush(Color.FromRgb(239, 244, 246));
    private static readonly Brush TableHeaderBrush = new SolidColorBrush(Color.FromRgb(250, 252, 253));
    private static readonly Brush TableBorderBrush = new SolidColorBrush(Color.FromRgb(221, 228, 234));

    public static FlowDocument Render(string markdown)
    {
        var document = new FlowDocument
        {
            FontFamily = PreviewFont,
            FontSize = 14,
            Foreground = TextBrush,
            PagePadding = new Thickness(30, 24, 30, 24)
        };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            document.Blocks.Add(new Paragraph(new Run("Nothing to preview"))
            {
                Foreground = MutedBrush,
                FontStyle = FontStyles.Italic
            });
            return document;
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var codeLines = new List<string>();
        var inFencedCodeBlock = false;
        System.Windows.Documents.List? activeList = null;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                if (inFencedCodeBlock)
                {
                    AddCodeBlock(document, codeLines);
                    codeLines.Clear();
                }
                else
                {
                    activeList = null;
                }

                inFencedCodeBlock = !inFencedCodeBlock;
                continue;
            }

            if (inFencedCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                activeList = null;
                continue;
            }

            if (TryReadTable(lines, lineIndex, out var tableRows, out var rowsConsumed))
            {
                activeList = null;
                AddTable(document, tableRows);
                lineIndex += rowsConsumed - 1;
                continue;
            }

            if (TryGetHeading(line, out var level, out var headingText))
            {
                activeList = null;
                AddHeading(document, level, headingText);
                continue;
            }

            if (TryGetBulletText(line, out var bulletText))
            {
                activeList ??= AddList(document);
                activeList.ListItems.Add(new ListItem(CreateParagraph(bulletText, new Thickness(0, 0, 0, 3))));
                continue;
            }

            activeList = null;
            document.Blocks.Add(CreateParagraph(line, new Thickness(0, 0, 0, 12)));
        }

        if (inFencedCodeBlock && codeLines.Count > 0)
        {
            AddCodeBlock(document, codeLines);
        }

        return document;
    }

    private static void AddTable(FlowDocument document, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var columnCount = rows.Max(row => row.Count);
        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 4, 0, 16)
        };

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            table.Columns.Add(new TableColumn());
        }

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var tableRow = new TableRow();
            rowGroup.Rows.Add(tableRow);

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var text = columnIndex < rows[rowIndex].Count ? rows[rowIndex][columnIndex] : string.Empty;
                var paragraph = new Paragraph(new Run(text))
                {
                    Margin = new Thickness(0),
                    LineHeight = 18
                };

                if (rowIndex == 0)
                {
                    paragraph.FontWeight = FontWeights.SemiBold;
                }

                var cell = new TableCell(paragraph)
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    BorderBrush = TableBorderBrush,
                    BorderThickness = new Thickness(1),
                    Background = rowIndex == 0 ? TableHeaderBrush : Brushes.Transparent
                };

                tableRow.Cells.Add(cell);
            }
        }

        document.Blocks.Add(table);
    }

    private static void AddHeading(FlowDocument document, int level, string text)
    {
        var paragraph = CreateParagraph(text, new Thickness(0, level == 1 ? 0 : 12, 0, 8));
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.FontSize = level switch
        {
            1 => 26,
            2 => 22,
            3 => 18,
            _ => 16
        };

        document.Blocks.Add(paragraph);
    }

    private static System.Windows.Documents.List AddList(FlowDocument document)
    {
        var list = new System.Windows.Documents.List
        {
            MarkerStyle = TextMarkerStyle.Disc,
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(22, 0, 0, 0)
        };
        document.Blocks.Add(list);
        return list;
    }

    private static void AddCodeBlock(FlowDocument document, IEnumerable<string> lines)
    {
        var paragraph = new Paragraph(new Run(string.Join(Environment.NewLine, lines)))
        {
            FontFamily = CodeFont,
            FontSize = 13,
            Background = CodeBackgroundBrush,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(12)
        };

        document.Blocks.Add(paragraph);
    }

    private static Paragraph CreateParagraph(string text, Thickness margin)
    {
        return new Paragraph(new Run(text.Trim()))
        {
            Margin = margin,
            LineHeight = 20
        };
    }

    private static bool TryGetHeading(string line, out int level, out string text)
    {
        var trimmed = line.TrimStart();
        level = 0;
        text = string.Empty;

        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level is < 1 or > 6 || level >= trimmed.Length || !char.IsWhiteSpace(trimmed[level]))
        {
            return false;
        }

        text = trimmed[level..].Trim();
        return text.Length > 0;
    }

    private static bool TryReadTable(string[] lines, int startIndex, out List<IReadOnlyList<string>> rows, out int rowsConsumed)
    {
        rows = new List<IReadOnlyList<string>>();
        rowsConsumed = 0;

        if (startIndex + 1 >= lines.Length)
        {
            return false;
        }

        var headerCells = ParseTableRow(lines[startIndex]);
        if (headerCells.Count < 2 || !IsTableSeparator(lines[startIndex + 1]))
        {
            return false;
        }

        rows.Add(headerCells);
        rowsConsumed = 2;

        for (var rowIndex = startIndex + 2; rowIndex < lines.Length; rowIndex++)
        {
            var rowCells = ParseTableRow(lines[rowIndex]);
            if (rowCells.Count == 0)
            {
                break;
            }

            rows.Add(rowCells);
            rowsConsumed++;
        }

        return true;
    }

    private static IReadOnlyList<string> ParseTableRow(string line)
    {
        if (!line.Contains('|'))
        {
            return Array.Empty<string>();
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        var cells = trimmed.Split('|')
            .Select(cell => cell.Trim())
            .ToArray();

        return cells.Any(cell => cell.Length > 0) ? cells : Array.Empty<string>();
    }

    private static bool IsTableSeparator(string line)
    {
        var cells = ParseTableRow(line);
        return cells.Count >= 2 && cells.All(cell =>
        {
            var normalized = cell.Trim().Trim(':');
            return normalized.Length >= 3 && normalized.All(character => character == '-');
        });
    }

    private static bool TryGetBulletText(string line, out string text)
    {
        var trimmed = line.TrimStart();
        text = string.Empty;

        if (trimmed.Length < 2 || (trimmed[0] != '-' && trimmed[0] != '*' && trimmed[0] != '+') || !char.IsWhiteSpace(trimmed[1]))
        {
            return false;
        }

        text = trimmed[2..].Trim();
        return text.Length > 0;
    }
}