using System.Text;
using MDRedactor.Core.Documents;

namespace MDRedactor.Core.Services;

public sealed class MarkdownFileService : IMarkdownFileService
{
    private const string Utf8EncodingName = "utf-8";
    private const string Windows1251EncodingName = "windows-1251";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

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
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Utf8EncodingName);
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
        return encodingName.Equals(Windows1251EncodingName, StringComparison.OrdinalIgnoreCase)
            ? Encoding.GetEncoding(1251)
            : Utf8WithoutBom;
    }
}

