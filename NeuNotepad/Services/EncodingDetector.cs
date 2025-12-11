using System.IO;
using System.Text;

namespace NeuNotepad.Services;

public static class EncodingDetector
{
    public static Encoding DetectEncoding(string filePath)
    {
        var bom = new byte[4];
        using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            file.Read(bom, 0, 4);
        }

        // UTF-32 BE
        if (bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
            return new UTF32Encoding(true, true);

        // UTF-32 LE
        if (bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
            return new UTF32Encoding(false, true);

        // UTF-8 BOM
        if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return Encoding.UTF8;

        // UTF-16 BE
        if (bom[0] == 0xFE && bom[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        // UTF-16 LE
        if (bom[0] == 0xFF && bom[1] == 0xFE)
            return Encoding.Unicode;

        // Try to detect UTF-8 without BOM
        if (IsUtf8(filePath))
            return new UTF8Encoding(false);

        // Default to system default encoding
        return Encoding.Default;
    }

    private static bool IsUtf8(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var text = Encoding.UTF8.GetString(bytes);
            var reEncoded = Encoding.UTF8.GetBytes(text);
            
            if (bytes.Length != reEncoded.Length)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != reEncoded[i])
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
