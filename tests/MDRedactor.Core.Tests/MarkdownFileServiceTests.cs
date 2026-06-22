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
}
