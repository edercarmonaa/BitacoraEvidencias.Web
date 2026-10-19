using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Oficios;
using BitacoraEvidencias.Web.Tests.Testing;
using BitacoraEvidencias.Web.Validation;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Validation;

public class FormRulesTests
{
    private static readonly DateTime Today = new(2026, 10, 19);
    private static readonly CaseFormValues Valid = new("AAAA900101HVZBBB00", "Persona de prueba", "30PBH0442H", 2020, 2021, new(2026, 3, 1));

    [Theory]
    [InlineData("30PBH0442H")]
    [InlineData("30PBH0260Z")]
    [InlineData("184-D2")]
    [InlineData("327-DV")]
    [InlineData(" 327-dv ")]
    public void AcceptsStandardAndHistoricalCct(string cct) => Assert.True(FormRules.IsCct(cct));

    [Theory]
    [InlineData("CCT001")]
    [InlineData("184D2")]
    [InlineData("327-DVV")]
    [InlineData("30PBH044H")]
    public void RejectsOtherCctFormats(string cct) => Assert.False(FormRules.IsCct(cct));

    [Theory]
    [InlineData("aaaa900101hvzbbb00", true)]
    [InlineData("AAAA000229HVZBBBA0", true)]
    [InlineData("AAAA000229HVZBBB00", false)]
    [InlineData("AAAA900231HVZBBB00", false)]
    [InlineData("AAAA901301HVZBBB00", false)]
    [InlineData("CURP00000000000001", false)]
    public void CurpChecksFormatAndCalendarWithoutConsultingRegistry(string curp, bool expected) => Assert.Equal(expected, FormRules.IsCurp(curp));

    [Fact]
    public void NewCaseRequiresAllSixConfirmedFields()
    {
        var errors = FormRules.ValidateCase(new(null, null, null, null, null, null), Today, today: Today);
        Assert.Equal(6, errors.Count);
        Assert.Contains(errors, error => error.Key == "FechaVerificacion");
        Assert.Empty(FormRules.ValidateCase(Valid, new(2026, 1, 1), today: Today));
    }

    [Fact]
    public void HistoricalIncompleteFieldsCanRemainUnchangedWhileOtherFieldsAreEdited()
    {
        var historical = new CaseFormValues(null, null, null, null, null, null);
        Assert.Empty(FormRules.ValidateCase(historical with { NombreCompleto = "Nombre capturado" }, Today, historical, Today));
        var historicalInvalid = Valid with { Curp = "CURP ANTIGUA", Cct = "ANTIGUA" };
        Assert.Empty(FormRules.ValidateCase(historicalInvalid, new(2026, 1, 1), historicalInvalid, Today));
        Assert.Contains(FormRules.ValidateCase(historicalInvalid with { Curp = "OTRA INVALIDA" }, Today, historicalInvalid, Today), error => error.Key == "Curp");
        Assert.Contains(FormRules.ValidateCase(Valid with { Curp = null }, Today, Valid, Today), error => error.Key == "Curp");
    }

    [Fact]
    public void DatesAndPeriodsEnforceBoundsAndCrossFieldOrder()
    {
        Assert.Contains(FormRules.ValidateCase(Valid with { PeriodoFin = 2019 }, new(2026, 1, 1), today: Today), error => error.Key == "PeriodoFin");
        Assert.Contains(FormRules.ValidateCase(Valid with { FechaVerificacion = Today.AddDays(1) }, Today, today: Today), error => error.Message.Contains("futura"));
        Assert.Contains(FormRules.ValidateCase(Valid, new(2026, 4, 1), today: Today), error => error.Message.Contains("anterior"));
        Assert.Empty(FormRules.ValidateCase(Valid with { FechaVerificacion = Today }, Today, today: Today));
        Assert.NotEmpty(FormRules.ValidateOfficeDate(Today.AddDays(1), today: Today));
        Assert.NotEmpty(FormRules.ValidateOfficeDate(Today, Today.AddDays(-2), [Today.AddDays(-1)], Today));
        Assert.Empty(FormRules.ValidateOfficeDate(Today.AddDays(1), Today.AddDays(1), today: Today));
    }

    [Theory]
    [InlineData("https://example.invalid/foto", true)]
    [InlineData("http://example.invalid", true)]
    [InlineData("ftp://example.invalid/foto", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", true)]
    public void EvidenceLinkAllowsOnlyOptionalHttpAndHttps(string value, bool valid) => Assert.Equal(valid, new HttpUrlAttribute().IsValid(value));

    [Fact]
    public async Task OfficeNumberIsUniqueIgnoringCaseOnBlurAndPost()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var office = new Oficio { NumeroOficio = "OF-001", Asunto = "Prueba", FechaOficio = Today };
        context.Oficios.Add(office);
        await context.SaveChangesAsync();
        var page = new CreateModel(context, new RecordingAuditLogService())
        {
            PageContext = new() { HttpContext = new DefaultHttpContext() },
            Input = new() { NumeroOficio = "of-001", Asunto = "Duplicado", FechaOficio = Today.AddDays(-1) }
        };
        Assert.IsType<string>((await page.OnGetValidateNumberAsync(" of-001 ")).Value);
        Assert.Equal(true, (await page.OnGetValidateNumberAsync("of-001", office.Id)).Value);
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.Contains("Input.NumeroOficio", page.ModelState.Keys);
        Assert.Equal(1, await context.Oficios.CountAsync());
    }

    [Fact]
    public async Task HistoricalEditUsesPersistedValuesAndRejectsNewInvalidDataWithoutSaving()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var office = new Oficio { NumeroOficio = "HISTORICO", Asunto = "Prueba", FechaOficio = Today.AddYears(-1) };
        var record = new CasoCorreccion { Oficio = office, Consecutivo = 1, NivelEducativo = "Primaria", TipoCorreccion = "Correccion" };
        context.CasosCorreccion.Add(record);
        await context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new CasoApplicationService(context, null!, audit, new CaseAuditService(audit, new HttpContextAccessor()));
        var updated = await service.UpdateAsync(new CasoUpdateRequest
        { Id = record.Id, NombreCompleto = "Nombre completado", NivelEducativo = "Primaria", TipoCorreccion = "Correccion", Estatus = record.Estatus });
        Assert.False(updated.HasValidationErrors);
        context.ChangeTracker.Clear();
        var saved = await context.CasosCorreccion.SingleAsync();
        Assert.Null(saved.Curp);
        Assert.Null(saved.FechaVerificacion);
        Assert.Equal("Nombre completado", saved.NombreCompleto);
        var rejected = await service.UpdateAsync(new CasoUpdateRequest
        { Id = record.Id, Curp = "INVALIDA", NombreCompleto = saved.NombreCompleto, NivelEducativo = "Primaria", TipoCorreccion = "Correccion", Estatus = saved.Estatus });
        Assert.True(rejected.HasValidationErrors);
        Assert.Contains(rejected.ValidationErrors, error => error.Key == "Curp");
        Assert.Null((await context.CasosCorreccion.AsNoTracking().SingleAsync()).Curp);
    }

    [Fact]
    public void DefaultValidationMessagesAreSpanishAndDoNotEchoAttemptedValues()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationRazorPages();
        using var provider = services.BuildServiceProvider();
        var metadata = provider.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForProperty(typeof(CreateModel.InputModel), nameof(CreateModel.InputModel.NumeroOficio));
        var length = Assert.Single(metadata.ValidatorMetadata.OfType<StringLengthAttribute>());
        Assert.Contains("admite hasta 80", length.FormatErrorMessage(metadata.GetDisplayName()));
        var options = new Microsoft.AspNetCore.Mvc.MvcOptions();
        SpanishValidationMessages.Configure(options);
        var error = options.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor("secret-input", "Fecha");
        Assert.Contains("Captura un valor valido", error);
        Assert.DoesNotContain("secret-input", error);
    }

    [Fact]
    public async Task HistoricalOfficeKeepsEmptySubjectThroughServerValidation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var office = new Oficio { NumeroOficio = "OF-HIST", Asunto = "", FechaOficio = Today.AddYears(-1) };
        context.Oficios.Add(office);
        await context.SaveChangesAsync();
        var application = new BlockingOfficeService();
        var page = new EditModel(context, application) { Input = new()
        { Id = office.Id, NumeroOficio = office.NumeroOficio, Asunto = null!, FechaOficio = office.FechaOficio } };
        page.ModelState.AddModelError("Input.Asunto", "El asunto es obligatorio.");
        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.True(application.Called);
        Assert.Empty(page.ModelState["Input.Asunto"]!.Errors);
        var audit = new RecordingAuditLogService();
        var service = new OficioApplicationService(context, null!, audit, new CaseAuditService(audit, new HttpContextAccessor()));
        var result = await service.UpdateAsync(new()
        { Id = office.Id, NumeroOficio = office.NumeroOficio, Asunto = null!, FechaOficio = office.FechaOficio, Notas = "Nota nueva" });
        Assert.False(result.HasValidationErrors);
        Assert.Null(result.ErrorMessage);
        var saved = await context.Oficios.AsNoTracking().SingleAsync();
        Assert.Equal("", saved.Asunto);
        Assert.Equal("Nota nueva", saved.Notas);
    }

    private sealed class BlockingOfficeService : IOficioApplicationService
    {
        public bool Called { get; private set; }
        public Task<OficioUpdateResult> UpdateAsync(OficioUpdateRequest request)
        { Called = true; return Task.FromResult(new OficioUpdateResult { ErrorMessage = "Retorno de prueba" }); }
        public Task<OficioDeleteResult> DeleteAsync(int id) => throw new NotSupportedException();
    }
}
