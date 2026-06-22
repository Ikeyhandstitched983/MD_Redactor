namespace MDRedactor.Core.EditTags;

public sealed class EditTagValidator
{
    private readonly EditTagParser _parser = new();

    public IReadOnlyList<EditDiagnostic> Validate(string markdown)
    {
        return _parser.Parse(markdown).Diagnostics;
    }
}

