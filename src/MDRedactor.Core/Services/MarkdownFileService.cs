using System.Text;
using MDRedactor.Core.Documents;

namespace MDRedactor.Core.Services;

public sealed class MarkdownFileService : IMarkdownFileService
{
    private const string Utf8EncodingName = "utf-8";
    private const string Utf8BomEncodingName = "utf-8-bom";
    private const string Utf16LeEncodingName = "utf-16le";
    private const string Utf16BeEncodingName = "utf-16be";
    private const string Windows1251EncodingName = "windows-1251";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UnicodeEncoding Utf16LeWithBom = new(bigEndian: false, byteOrderMark: true);
    private static readonly UnicodeEncoding Utf16BeWithBom = new(bigEndian: true, byteOrderMark: true);

    static MarkdownFileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<MarkdownDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var (markdown, encodingName) = Decode(bytes);

        return new MarkdownDocument(filePath, markdown, encodingName);
    }

    public Task WriteAsync(MarkdownDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var encoding = ResolveEncoding(document.EncodingName);
        return File.WriteAllTextAsync(document.FilePath, document.Markdown, encoding, cancellationToken);
    }

    private static (string Markdown, string EncodingName) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Utf8BomEncodingName);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Utf16LeEncodingName);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Utf16BeEncodingName);
        }

        try
        {
            return (StrictUtf8.GetString(bytes), Utf8EncodingName);
        }
        catch (DecoderFallbackException)
        {
            var windows1251 = Encoding.GetEncoding(1251);
            return (windows1251.GetString(bytes), Windows1251EncodingName);
        }
    }

    private static Encoding ResolveEncoding(string encodingName)
    {
        return encodingName.ToLowerInvariant() switch
        {
            Utf8BomEncodingName => Utf8WithBom,
            Utf16LeEncodingName => Utf16LeWithBom,
            Utf16BeEncodingName => Utf16BeWithBom,
            Windows1251EncodingName => Encoding.GetEncoding(1251),
            _ => Utf8WithoutBom
        };
    }
}
