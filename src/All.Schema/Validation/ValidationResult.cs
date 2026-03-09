namespace All.Schema.Validation;

/// <summary>
/// The result of schema validation — either success or a list of errors.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>The validation errors found. Empty if validation passed.</summary>
    public IReadOnlyList<SchemaError> Errors { get; }

    /// <summary>Whether validation passed with no errors.</summary>
    public bool IsValid => Errors.Count == 0;

    private ValidationResult(IReadOnlyList<SchemaError> errors)
    {
        Errors = errors;
    }

    /// <summary>Creates a successful validation result with no errors.</summary>
    public static ValidationResult Success() => new([]);

    /// <summary>Creates a failed validation result with the given errors.</summary>
    public static ValidationResult Failure(IReadOnlyList<SchemaError> errors) => new(errors);
}
