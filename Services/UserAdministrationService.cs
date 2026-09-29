using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IUserAdministrationService
{
    Task<CreateUserResult> CreateAsync(string username, string role);
    Task<ResetPasswordResult> ResetPasswordAsync(int id);
    Task<ToggleUserActiveResult> ToggleActiveAsync(int id, int? currentUserId);
    Task<UpdateUserRoleResult> UpdateRoleAsync(int id, string role);
}

public sealed class CreateUserResult : ApplicationCommandResult
{
    public string Username { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}

public sealed class ResetPasswordResult : ApplicationCommandResult
{
    public string Username { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}

public sealed class ToggleUserActiveResult : ApplicationCommandResult
{
    public string Username { get; init; } = string.Empty;
    public bool Active { get; init; }
}

public sealed class UpdateUserRoleResult : ApplicationCommandResult
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public sealed class UserAdministrationService(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : IUserAdministrationService
{
    private static readonly IReadOnlyList<string> AllowedRoles = [SecurityDefaults.AdminRole, SecurityDefaults.CapturistaRole];

    public async Task<CreateUserResult> CreateAsync(string username, string role)
    {
        var normalizedRole = role?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRole) || !AllowedRoles.Contains(normalizedRole))
        {
            return new CreateUserResult
            {
                ValidationErrors = [new("Role", "Rol no valido.")]
            };
        }

        var normalizedUsername = username.Trim();
        var normalized = normalizedUsername.ToUpperInvariant();

        var exists = await dbContext.UsuariosSistema
            .AnyAsync(x => x.UsuarioNormalizado == normalized);

        if (exists)
        {
            return new CreateUserResult
            {
                ValidationErrors = [new("Username", "Ya existe un usuario con ese nombre.")]
            };
        }

        var tempPassword = TemporaryPasswordGenerator.Generate(12);
        var (hash, salt) = PasswordHasher.HashPassword(tempPassword);

        var user = new UsuarioSistema
        {
            Usuario = normalizedUsername,
            UsuarioNormalizado = normalized,
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = normalizedRole,
            Activo = true,
            MustChangePassword = true,
            CreadoEnUtc = DateTime.UtcNow
        };

        dbContext.UsuariosSistema.Add(user);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("UsuariosSistema.UsuarioNormalizado"))
        {
            return new CreateUserResult
            {
                ValidationErrors = [new("Username", "Ya existe un usuario con ese nombre.")]
            };
        }
        catch (DbUpdateException)
        {
            return new CreateUserResult
            {
                ErrorMessage = "No se pudo crear el usuario. Intenta nuevamente."
            };
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Create,
            EntityType = "Usuario",
            EntityId = user.Id,
            Username = user.Usuario,
            Role = user.Rol,
            Details = "Alta de usuario por admin. Contrasena temporal generada.",
            Success = true
        });

        return new CreateUserResult
        {
            Username = user.Usuario,
            TemporaryPassword = tempPassword
        };
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(int id)
    {
        var user = await dbContext.UsuariosSistema.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return new ResetPasswordResult { NotFound = true };
        }

        var tempPassword = TemporaryPasswordGenerator.Generate(12);
        var (hash, salt) = PasswordHasher.HashPassword(tempPassword);

        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.MustChangePassword = true;
        user.IntentosFallidos = 0;
        user.BloqueadoHastaUtc = null;
        user.PasswordUpdatedAtUtc = null;

        await dbContext.SaveChangesAsync();

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Update,
            EntityType = "Usuario",
            EntityId = user.Id,
            Username = user.Usuario,
            Role = user.Rol,
            Details = "Reset de contrasena temporal por admin.",
            Success = true
        });

        return new ResetPasswordResult
        {
            Username = user.Usuario,
            TemporaryPassword = tempPassword
        };
    }

    public async Task<ToggleUserActiveResult> ToggleActiveAsync(int id, int? currentUserId)
    {
        var user = await dbContext.UsuariosSistema.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return new ToggleUserActiveResult { NotFound = true };
        }

        if (currentUserId == user.Id && user.Activo)
        {
            return new ToggleUserActiveResult
            {
                ErrorMessage = "No puedes desactivar tu propio usuario."
            };
        }

        var newActive = !user.Activo;
        if (!newActive && user.Rol == SecurityDefaults.AdminRole)
        {
            var activeAdmins = await dbContext.UsuariosSistema
                .CountAsync(x => x.Activo && x.Rol == SecurityDefaults.AdminRole);

            if (activeAdmins <= 1)
            {
                return new ToggleUserActiveResult
                {
                    ErrorMessage = "Debe existir al menos un admin activo."
                };
            }
        }

        user.Activo = newActive;
        await dbContext.SaveChangesAsync();

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Update,
            EntityType = "Usuario",
            EntityId = user.Id,
            Username = user.Usuario,
            Role = user.Rol,
            Details = newActive ? "Usuario reactivado por admin." : "Usuario desactivado por admin.",
            Success = true
        });

        return new ToggleUserActiveResult
        {
            Username = user.Usuario,
            Active = newActive
        };
    }

    public async Task<UpdateUserRoleResult> UpdateRoleAsync(int id, string role)
    {
        var normalizedRole = role?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRole) || !AllowedRoles.Contains(normalizedRole))
        {
            return new UpdateUserRoleResult
            {
                ErrorMessage = "Rol no valido."
            };
        }

        var user = await dbContext.UsuariosSistema.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return new UpdateUserRoleResult { NotFound = true };
        }

        if (user.Rol == normalizedRole)
        {
            return new UpdateUserRoleResult
            {
                Username = user.Usuario,
                Role = normalizedRole
            };
        }

        if (user.Rol == SecurityDefaults.AdminRole && normalizedRole != SecurityDefaults.AdminRole && user.Activo)
        {
            var activeAdmins = await dbContext.UsuariosSistema
                .CountAsync(x => x.Activo && x.Rol == SecurityDefaults.AdminRole);

            if (activeAdmins <= 1)
            {
                return new UpdateUserRoleResult
                {
                    ErrorMessage = "Debe existir al menos un admin activo."
                };
            }
        }

        var oldRole = user.Rol;
        user.Rol = normalizedRole;
        await dbContext.SaveChangesAsync();

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Update,
            EntityType = "Usuario",
            EntityId = user.Id,
            Username = user.Usuario,
            Role = normalizedRole,
            Details = $"Cambio de rol de {oldRole} a {normalizedRole}.",
            Success = true
        });

        return new UpdateUserRoleResult
        {
            Username = user.Usuario,
            Role = normalizedRole
        };
    }
}
