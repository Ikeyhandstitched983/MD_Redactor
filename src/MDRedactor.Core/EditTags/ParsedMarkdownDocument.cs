namespace MDRedactor.Core.EditTags;

public sealed record ParsedMarkdownDocument
{
    public required string OriginalMarkdown { get; init; }

    public required string MarkdownWithoutEditMarkup { get; init; }

    public required IReadOnlyList<EditAnnotation> Edits { get; init; }

    public required IReadOnlyList<EditDiagnostic> Diagnostics { get; init; }

    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == EditDiagnosticSeverity.Error);
}

