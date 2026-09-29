using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var requireHttpsCookies = builder.Configuration.GetValue<bool>("Security:RequireHttpsCookies");
var enableHttpsRedirection = builder.Configuration.GetValue<bool>("Security:EnableHttpsRedirection");
var prepareApplicationData = builder.Configuration.GetValue<bool>("Startup:PrepareApplicationData");
var exitAfterPrepareApplicationData = builder.Configuration.GetValue<bool>("Startup:ExitAfterPrepareApplicationData");
var resetApplicationData = builder.Configuration.GetValue<bool>("Startup:ResetApplicationData");
var protectExistingData = builder.Configuration.GetValue<bool>("Security:ProtectExistingData");
var checkLegacySchemaOnly = builder.Configuration.GetValue<bool>("Startup:CheckLegacySchemaOnly");
var stampLegacyBaselineOnly = builder.Configuration.GetValue<bool>("Startup:StampLegacyBaselineOnly");

builder.Services.AddApplicationRazorPages();
builder.Services.AddApplicationAuthentication(requireHttpsCookies);
builder.Services.AddApplicationAuthorization();
builder.Services.AddSensitiveDataProtection(builder.Configuration);
builder.Services.AddValidatedFileStorageOptions(builder.Configuration);
builder.Services.AddSingleton<SqlitePragmaConnectionInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<SqlitePragmaConnectionInterceptor>());
});
builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ICaseAuditService, CaseAuditService>();
builder.Services.AddScoped<IOficioApplicationService, OficioApplicationService>();
builder.Services.AddScoped<ICasoApplicationService, CasoApplicationService>();
builder.Services.AddScoped<IEvidenciaApplicationService, EvidenciaApplicationService>();
builder.Services.AddScoped<IUserAdministrationService, UserAdministrationService>();
builder.Services.AddScoped<ISqlAdminService, SqlAdminService>();
builder.Services.AddScoped<IOficioImportApplicationService, OficioImportApplicationService>();
builder.Services.AddScoped<IOficioImportPreviewService, OficioImportPreviewService>();
builder.Services.AddScoped<ICasoImportApplicationService, CasoImportApplicationService>();
builder.Services.AddScoped<ICasoImportPreviewService, CasoImportPreviewService>();
builder.Services.AddScoped<IEvidenciaImportApplicationService, EvidenciaImportApplicationService>();
builder.Services.AddScoped<IEvidenciaImportPreviewService, EvidenciaImportPreviewService>();
builder.Services.AddSingleton<ITabularImportFileParser, TabularImportFileParser>();
builder.Services.AddSingleton<IImportPreviewStore, ImportPreviewStore>();
builder.Services.AddHttpContextAccessor();

if (prepareApplicationData)
{
    builder.EnsureSqliteFolderExistsForStartup();
}

var app = builder.Build();

if (checkLegacySchemaOnly)
{
    ApplicationDataStartup.CheckLegacySchemaState(app, builder.Configuration);
    return;
}

if (stampLegacyBaselineOnly)
{
    ApplicationDataStartup.StampLegacySchemaBaseline(app, builder.Configuration);
    return;
}

app.UseProductionExceptionHandling();
app.InitializeStorageRoot();

// Serve static files from wwwroot (css/js/libs/images).
app.UseConfiguredHttpsRedirection(enableHttpsRedirection);
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseApplicationUserSecurityRefresh();
app.UseAuthorization();
ApplicationDataStartup.InitializeApplicationData(
    app,
    builder.Configuration,
    prepareApplicationData,
    resetApplicationData);

if (protectExistingData)
{
    using var scope = app.Services.CreateScope();
    var migration = scope.ServiceProvider.GetRequiredService<SensitiveDataMigrationService>();
    var result = await migration.ProtectExistingDataAsync();
    app.Logger.LogInformation(
        "Proteccion manual completada. Carpetas={StorageFolders}, Casos={Cases}, Auditoria={AuditLogs}, Evidencias={EvidenceFiles}, Errores={Errors}",
        result.StorageFoldersProcessed,
        result.CasesProcessed,
        result.AuditLogsProcessed,
        result.EvidenceFilesProcessed,
        result.Errors.Count);

    foreach (var error in result.Errors)
    {
        app.Logger.LogWarning("Proteccion manual con advertencia: {Error}", error);
    }

    return;
}

if (prepareApplicationData && exitAfterPrepareApplicationData)
{
    app.Logger.LogInformation("Preparacion explicita de datos completada. Saliendo sin iniciar el servidor.");
    return;
}

app.MapRazorPages();

app.Run();



