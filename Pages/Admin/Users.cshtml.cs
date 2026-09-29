using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class UsersModel(
    AppDbContext dbContext,
    IUserAdministrationService userAdministrationService) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [BindProperty]
    public CreateUserInput NewUser { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    public List<UserRow> Users { get; private set; } = [];
    public IReadOnlyList<string> Roles { get; } = [SecurityDefaults.AdminRole, SecurityDefaults.CapturistaRole];
    public int TotalRows { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public int MaxAllowedPageSize => MaxPageSize;

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task OnGetAsync()
    {
        await LoadUsersAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!Roles.Contains(NewUser.Role))
        {
            ModelState.AddModelError(nameof(NewUser.Role), "Rol no valido.");
        }

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync();
            return Page();
        }

        var result = await userAdministrationService.CreateAsync(NewUser.Username, NewUser.Role);

        foreach (var validationError in result.ValidationErrors)
        {
            var key = string.IsNullOrWhiteSpace(validationError.Key)
                ? string.Empty
                : $"{nameof(NewUser)}.{validationError.Key}";
            ModelState.AddModelError(key, validationError.Message);
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage);
        }

        if (result.HasValidationErrors || !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await LoadUsersAsync();
            return Page();
        }

        FlashSuccess = $"Usuario '{result.Username}' creado. Contrasena temporal: {result.TemporaryPassword}";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int id)
    {
        var result = await userAdministrationService.ResetPasswordAsync(id);
        if (result.NotFound)
        {
            FlashError = "Usuario no encontrado.";
            return RedirectToCurrentPage();
        }

        FlashSuccess = $"Contrasena temporal regenerada para '{result.Username}': {result.TemporaryPassword}";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var result = await userAdministrationService.ToggleActiveAsync(id, GetCurrentUserId());
        if (result.NotFound)
        {
            FlashError = "Usuario no encontrado.";
            return RedirectToCurrentPage();
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            FlashError = result.ErrorMessage;
            return RedirectToCurrentPage();
        }

        FlashSuccess = result.Active
            ? $"Usuario '{result.Username}' activado."
            : $"Usuario '{result.Username}' desactivado.";
        return RedirectToCurrentPage();
    }

    public async Task<IActionResult> OnPostUpdateRoleAsync(int id, string role)
    {
        var normalizedRole = role?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRole) || !Roles.Contains(normalizedRole))
        {
            FlashError = "Rol no valido.";
            return RedirectToCurrentPage();
        }

        var result = await userAdministrationService.UpdateRoleAsync(id, normalizedRole);
        if (result.NotFound)
        {
            FlashError = "Usuario no encontrado.";
            return RedirectToCurrentPage();
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            FlashError = result.ErrorMessage;
            return RedirectToCurrentPage();
        }

        if (string.IsNullOrWhiteSpace(result.Username))
        {
            return RedirectToCurrentPage();
        }

        FlashSuccess = $"Rol actualizado para '{result.Username}' a '{result.Role}'.";
        return RedirectToCurrentPage();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }

    private async Task LoadUsersAsync()
    {
        PageSize = NormalizePageSize(PageSize);
        PageNumber = PageNumber <= 0 ? 1 : PageNumber;

        var query = dbContext.UsuariosSistema
            .AsNoTracking();

        TotalRows = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRows / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        Users = await query
            .OrderBy(x => x.Usuario)
            .Skip(skip)
            .Take(PageSize)
            .Select(x => new UserRow
            {
                Id = x.Id,
                Username = x.Usuario,
                Role = x.Rol,
                Active = x.Activo,
                MustChangePassword = x.MustChangePassword,
                FailedAttempts = x.IntentosFallidos,
                LockedUntilUtc = x.BloqueadoHastaUtc,
                CreatedAtUtc = x.CreadoEnUtc,
                LastAccessUtc = x.UltimoAccesoUtc,
                PasswordUpdatedAtUtc = x.PasswordUpdatedAtUtc
            })
            .ToListAsync();
    }

    private int NormalizePageSize(int pageSize) =>
        Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

    private RedirectToPageResult RedirectToCurrentPage() =>
        RedirectToPage("/Admin/Users", new
        {
            PageNumber = PageNumber <= 0 ? 1 : PageNumber,
            PageSize = NormalizePageSize(PageSize)
        });

    public class CreateUserInput
    {
        [Required(ErrorMessage = "El usuario es obligatorio.")]
        [StringLength(60, MinimumLength = 3)]
        [Display(Name = "Usuario")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un rol.")]
        [Display(Name = "Rol")]
        public string Role { get; set; } = SecurityDefaults.CapturistaRole;
    }

    public class UserRow
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public bool Active { get; init; }
        public bool MustChangePassword { get; init; }
        public int FailedAttempts { get; init; }
        public DateTime? LockedUntilUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? LastAccessUtc { get; init; }
        public DateTime? PasswordUpdatedAtUtc { get; init; }

        public DateTime CreatedAtLocal => DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc).ToLocalTime();
        public DateTime? LastAccessLocal => LastAccessUtc.HasValue
            ? DateTime.SpecifyKind(LastAccessUtc.Value, DateTimeKind.Utc).ToLocalTime()
            : null;
        public DateTime? PasswordUpdatedLocal => PasswordUpdatedAtUtc.HasValue
            ? DateTime.SpecifyKind(PasswordUpdatedAtUtc.Value, DateTimeKind.Utc).ToLocalTime()
            : null;
        public DateTime? LockedUntilLocal => LockedUntilUtc.HasValue
            ? DateTime.SpecifyKind(LockedUntilUtc.Value, DateTimeKind.Utc).ToLocalTime()
            : null;
    }
}
