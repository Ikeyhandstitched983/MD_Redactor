namespace MDRedactor.Core.EditTags;

public sealed record EditAnnotation
{
    public required int Id { get; init; }

    public required string FragmentMarkdown { get; init; }

    public required string FragmentPlainText { get; init; }

    public required string Comment { get; init; }

    public required int RawStartIndex { get; init; }

    public required int RawEndIndex { get; init; }

    public required int FragmentRawStartIndex { get; init; }

    public required int FragmentRawEndIndex { get; init; }

    public required EditAnnotationKind Kind { get; init; }

    public bool IsInlineCandidate => Kind == EditAnnotationKind.Inline;
}

