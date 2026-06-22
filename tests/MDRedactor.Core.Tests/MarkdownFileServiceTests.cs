using System.Text;
using MDRedactor.Core.Documents;
using MDRedactor.Core.Services;

namespace MDRedactor.Core.Tests;

public sealed class MarkdownFileServiceTests
{
    [Fact]
    public async Task ReadAsync_PreservesUtf8CyrillicMarkdown()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var service = new MarkdownFileService();
        var expected = "# Заголовок\r\n\r\nТекст с кириллицей и правками.";

        try
        {
            await service.WriteAsync(new MarkdownDocument(filePath, expected, "utf-8"));

            var document = await service.ReadAsync(filePath);

            Assert.Equal(expected, document.Markdown);
            Assert.Equal("utf-8", document.EncodingName);
            Assert.Equal(Path.GetFileName(filePath), document.FileName);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_DetectsWindows1251Markdown()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var service = new MarkdownFileService();
        var expected = "# Сцена\r\n\r\nТекст в Windows-1251.";
        var encoding = Encoding.GetEncoding(1251);

        try
        {
            await File.WriteAllTextAsync(filePath, expected, encoding);

            var document = await service.ReadAsync(filePath);

            Assert.Equal(expected, document.Markdown);
            Assert.Equal("windows-1251", document.EncodingName);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_DetectsUtf8BomAndWriteAsync_PreservesBom()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var service = new MarkdownFileService();
        var expected = "# UTF-8 BOM\r\n\r\nРусский текст.";

        try
        {
            await File.WriteAllTextAsync(filePath, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            var document = await service.ReadAsync(filePath);
            await service.WriteAsync(document with { Markdown = document.Markdown + "\r\nПродолжение." });
            var bytes = await File.ReadAllBytesAsync(filePath);

            Assert.Equal("utf-8-bom", document.EncodingName);
            Assert.Equal(expected, document.Markdown);
            Assert.True(bytes.AsSpan(0, 3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_DetectsUtf16BomAndWriteAsync_PreservesEncoding()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var service = new MarkdownFileService();
        var expected = "# UTF-16\r\n\r\nРусский текст.";

        try
        {
            await File.WriteAllTextAsync(filePath, expected, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

            var document = await service.ReadAsync(filePath);
            await service.WriteAsync(document with { Markdown = document.Markdown + "\r\nПродолжение." });
            var bytes = await File.ReadAllBytesAsync(filePath);

            Assert.Equal("utf-16le", document.EncodingName);
            Assert.Equal(expected, document.Markdown);
            Assert.True(bytes.AsSpan(0, 2).SequenceEqual(new byte[] { 0xFF, 0xFE }));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_DetectsUtf16BigEndianBom()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
        var service = new MarkdownFileService();
        var expected = "# UTF-16 BE\r\n\r\nРусский текст.";

        try
        {
            await File.WriteAllTextAsync(filePath, expected, new UnicodeEncoding(bigEndian: true, byteOrderMark: true));

            var document = await service.ReadAsync(filePath);

            Assert.Equal("utf-16be", document.EncodingName);
            Assert.Equal(expected, document.Markdown);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
