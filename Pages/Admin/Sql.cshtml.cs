using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BitacoraEvidencias.Web.Pages.Admin;

[Authorize(Roles = SecurityDefaults.AdminRole)]
public class SqlModel(ISqlAdminService sqlAdminService) : PageModel
{
    private static readonly int[] AllowedLimits = [100, 500, 1000];

    [BindProperty]
    public SqlInput Input { get; set; } = new()
    {
        RowLimit = 100
    };

    public IReadOnlyList<int> AllowedRowLimits => AllowedLimits;
    public SqlExecutionResult? Result { get; private set; }

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostExecuteAsync(CancellationToken cancellationToken)
    {
        if (!AllowedLimits.Contains(Input.RowLimit))
        {
            ModelState.AddModelError(nameof(Input.RowLimit), "Selecciona un limite valido.");
        }

        if (RequiresConfirmation(Input.Sql) && !Input.ConfirmWrite)
        {
            ModelState.AddModelError(nameof(Input.ConfirmWrite), "Confirma que entiendes que la operacion modifica datos.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Result = await sqlAdminService.ExecuteAsync(Input.Sql, Input.RowLimit, cancellationToken);

        foreach (var validationError in Result.ValidationErrors)
        {
            var key = string.IsNullOrWhiteSpace(validationError.Key)
                ? string.Empty
                : $"{nameof(Input)}.{validationError.Key}";
            ModelState.AddModelError(key, validationError.Message);
        }

        if (!string.IsNullOrWhiteSpace(Result.ErrorMessage))
        {
            FlashError = Result.ErrorMessage;
            return Page();
        }

        if (Result.HasValidationErrors)
        {
            return Page();
        }

        FlashSuccess = string.Equals(Result.StatementType, "SELECT", StringComparison.OrdinalIgnoreCase)
            ? $"Consulta ejecutada. Filas devueltas: {Result.ReturnedRows}."
            : $"Sentencia {Result.StatementType} ejecutada. Filas afectadas: {Result.AffectedRows ?? 0}.";

        return Page();
    }

    private static bool RequiresConfirmation(string sql)
    {
        var statementType = sql?.TrimStart();
        return statementType is not null &&
               (statementType.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                statementType.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                statementType.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase));
    }

    public class SqlInput
    {
        [Required(ErrorMessage = "La sentencia SQL es obligatoria.")]
        [Display(Name = "SQL")]
        public string Sql { get; set; } = string.Empty;

        [Display(Name = "Limite de filas")]
        public int RowLimit { get; set; } = 100;

        [Display(Name = "Confirmo que existe respaldo reciente y que documentare motivo, fecha y responsable de esta operacion")]
        public bool ConfirmWrite { get; set; }
    }
}
