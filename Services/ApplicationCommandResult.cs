namespace BitacoraEvidencias.Web.Services;

public record ApplicationValidationError(string Key, string Message);

public abstract class ApplicationCommandResult
{
    public bool NotFound { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }
    public IReadOnlyList<ApplicationValidationError> ValidationErrors { get; init; } = [];
    public bool HasValidationErrors => ValidationErrors.Count > 0;
    public bool Succeeded => !NotFound && !HasValidationErrors && string.IsNullOrWhiteSpace(ErrorMessage);
}
