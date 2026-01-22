using System.IO;
using System.Text;
using System.Buffers;

namespace NeuNotepad.Services;

public static class EncodingDetector
{
    public static Encoding DetectEncoding(string filePath)
    {
        var bom = new byte[4];
        using (var file = new FileStream(
                   filePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite,
                   bufferSize: 4096,
                   options: FileOptions.SequentialScan))
        {
            _ = file.Read(bom, 0, 4);
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
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

        // Default to system default encoding
        return Encoding.Default;
    }

    private static bool IsUtf8(string filePath)
    {
        try
        {
            // Strict UTF-8 validation without reading the whole file into memory.
            // This streams the file once and fails fast on invalid byte sequences.
            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var decoder = utf8Strict.GetDecoder();

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            byte[] byteBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            char[] charBuffer = ArrayPool<char>.Shared.Rent(utf8Strict.GetMaxCharCount(byteBuffer.Length));
            try
            {
                while (true)
                {
                    var bytesRead = stream.Read(byteBuffer, 0, byteBuffer.Length);
                    if (bytesRead <= 0)
                        break;

                    decoder.Convert(
                        byteBuffer,
                        0,
                        bytesRead,
                        charBuffer,
                        0,
                        charBuffer.Length,
                        flush: false,
                        out _,
                        out _,
                        out _);
                }

                // Flush any trailing state (e.g., incomplete sequences)
                decoder.Convert(
                    Array.Empty<byte>(),
                    0,
                    0,
                    charBuffer,
                    0,
                    charBuffer.Length,
                    flush: true,
                    out _,
                    out _,
                    out _);

                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(byteBuffer);
                ArrayPool<char>.Shared.Return(charBuffer);
            }
        }
        catch
        {
            return false;
        }
    }
}
