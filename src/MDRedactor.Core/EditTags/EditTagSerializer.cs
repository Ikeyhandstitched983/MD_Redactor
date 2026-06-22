using System.Text;

namespace MDRedactor.Core.EditTags;

public sealed class EditTagSerializer
{
    private const string NewLine = "\r\n";
    private readonly EditTagParser _parser = new();

    public string BuildEditBlock(int id, string fragmentMarkdown, string comment)
    {
        ThrowIfInvalidId(id);

        var fragment = NormalizeLineEndings(fragmentMarkdown ?? string.Empty);
        var safeComment = SanitizeComment(comment ?? string.Empty);
        var builder = new StringBuilder();

        builder.Append("<!-- ed-start id=\"").Append(id).Append("\" -->").Append(NewLine);
        builder.Append(fragment);
        if (!EndsWithLineBreak(fragment))
        {
            builder.Append(NewLine);
        }

        builder.Append("<!-- ed-comm id=\"").Append(id).Append('"').Append(NewLine);
        builder.Append(safeComment);
        if (!EndsWithLineBreak(safeComment))
        {
            builder.Append(NewLine);
        }

        builder.Append("-->").Append(NewLine);
        builder.Append("<!-- ed-end id=\"").Append(id).Append("\" -->");

        return builder.ToString();
    }

    public string UpdateComment(string markdown, int id, string newComment)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ThrowIfInvalidId(id);

        var span = FindValidSpan(markdown, id);
        var replacement = BuildCommentReplacement(markdown, span, newComment);
        return ReplaceRange(markdown, span.CommentRawStartIndex, span.CommentRawEndIndex, replacement);
    }

    public string RemoveEditKeepFragment(string markdown, int id)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ThrowIfInvalidId(id);

        var span = FindValidSpan(markdown, id);
        var edit = span.Annotation;

        var builder = new StringBuilder(markdown.Length - (edit.RawEndIndex - edit.RawStartIndex) + edit.FragmentMarkdown.Length);
        builder.Append(markdown, 0, edit.RawStartIndex);
        builder.Append(markdown, edit.FragmentRawStartIndex, edit.FragmentRawEndIndex - edit.FragmentRawStartIndex);
        builder.Append(markdown, edit.RawEndIndex, markdown.Length - edit.RawEndIndex);

        return builder.ToString();
    }

    public string NormalizeEditMarkup(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var result = markdown;
        var spans = _parser.ParseDetailed(markdown)
            .Spans
            .Where(span => span.IsStructurallyValid)
            .OrderByDescending(span => span.Annotation.RawStartIndex);

        foreach (var span in spans)
        {
            var replacement = BuildCommentReplacement(result, span, span.Annotation.Comment);
            result = ReplaceRange(result, span.CommentRawStartIndex, span.CommentRawEndIndex, replacement);
        }

        return result;
    }

    private EditAnnotationSpan FindValidSpan(string markdown, int id)
    {
        var parseResult = _parser.ParseDetailed(markdown);
        if (parseResult.Document.HasErrors)
        {
            var message = parseResult.Document.Diagnostics.First(diagnostic => diagnostic.Severity == EditDiagnosticSeverity.Error).Message;
            throw new InvalidOperationException($"Markdown содержит ошибки разметки правок: {message}");
        }

        var span = parseResult.Spans.SingleOrDefault(candidate => candidate.IsStructurallyValid && candidate.Annotation.Id == id);
        return span ?? throw new InvalidOperationException($"Правка с id {id} не найдена.");
    }

    private static string BuildCommentReplacement(string markdown, EditAnnotationSpan span, string newComment)
    {
        var oldRawComment = markdown[span.CommentRawStartIndex..span.CommentRawEndIndex];
        var trailingLineBreak = GetSingleTrailingLineBreak(oldRawComment) ?? NewLine;
        return SanitizeComment(newComment ?? string.Empty) + trailingLineBreak;
    }

    private static string SanitizeComment(string comment)
    {
        return NormalizeLineEndings(comment)
            .Replace("--", "- -", StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", NewLine, StringComparison.Ordinal);
    }

    private static string? GetSingleTrailingLineBreak(string value)
    {
        if (value.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        if (value.EndsWith('\n'))
        {
            return "\n";
        }

        return value.EndsWith('\r') ? "\r" : null;
    }

    private static bool EndsWithLineBreak(string value)
    {
        return value.EndsWith('\n') || value.EndsWith('\r');
    }

    private static string ReplaceRange(string value, int startIndex, int endIndex, string replacement)
    {
        return value[..startIndex] + replacement + value[endIndex..];
    }

    private static void ThrowIfInvalidId(int id)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Id правки должен быть положительным целым числом.");
        }
    }
}

