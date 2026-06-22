namespace MDRedactor.Core.Documents;

public sealed record MarkdownDocument(string FilePath, string Markdown, string EncodingName)
{
    public string FileName => Path.GetFileName(FilePath);
}

