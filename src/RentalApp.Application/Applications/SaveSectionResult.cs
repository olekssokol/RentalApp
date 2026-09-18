namespace RentalApp.Application.Applications;

/// <summary>
/// Outcome of persisting a wizard section. Access failures use Result.Failure;
/// this type is returned only when data was (or need not be) written.
/// </summary>
public sealed class SaveSectionResult
{
    private SaveSectionResult(bool isValid, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        IsValid = isValid;
        FieldErrors = fieldErrors;
    }

    public bool IsValid { get; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    public static SaveSectionResult Valid() =>
        new(true, new Dictionary<string, string[]>(StringComparer.Ordinal));

    public static SaveSectionResult Invalid(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(false, fieldErrors);
}

public sealed class ResidenceSaveResult
{
    public ResidenceSaveResult(int residenceId, bool isValid, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        ResidenceId = residenceId;
        IsValid = isValid;
        FieldErrors = fieldErrors;
    }

    public int ResidenceId { get; }
    public bool IsValid { get; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }
}
