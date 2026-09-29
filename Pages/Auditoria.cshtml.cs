using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages;

public class AuditoriaModel(
    AppDbContext dbContext,
    ICaseAuditService caseAuditService) : PageModel
{
    private const int OfficePreviewLimit = 5;

    [BindProperty(SupportsGet = true, Name = "casoId")]
    public int? SelectedCaseId { get; set; }

    [BindProperty(SupportsGet = true, Name = "evidencia")]
    public string? SelectedEvidenceKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "numeroOficio")]
    public string NumeroOficioFiltro { get; set; } = string.Empty;

    [BindProperty]
    public NoCoincidenteInputModel NoCoincidente { get; set; } = new();

    public List<OficioAuditGroup> Oficios { get; private set; } = [];
    public IReadOnlyList<OficioAuditGroup> VisibleOficios { get; private set; } = [];
    public SelectedCaseAuditView? SelectedCase { get; private set; }
    public int TotalOficiosPendientes { get; private set; }
    public int TotalOficiosAuditables { get; private set; }
    public int OfficePreviewSize => OfficePreviewLimit;
    public bool HasNumeroOficioFiltro => !string.IsNullOrWhiteSpace(NumeroOficioFiltro);

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCoincidenteAsync(int caseId, CancellationToken cancellationToken)
    {
        var caso = await dbContext.CasosCorreccion
            .Include(x => x.Oficio)
            .Include(x => x.Evidencias)
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);

        if (caso is null || caso.Oficio is null)
        {
            return NotFound();
        }

        if (!await IsOficioAuditableAsync(caso.OficioId, cancellationToken) ||
            !CatalogosAuditoriaCaso.TieneEvidenciaRevisable(caso.EvidenciaUrl, caso.Evidencias.Count))
        {
            FlashError = "El caso ya no esta disponible para auditoria.";
            return RedirectToPage(new { numeroOficio = NumeroOficioFiltro });
        }

        caseAuditService.MarkCoincidente(caso);
        await dbContext.SaveChangesAsync(cancellationToken);
        await caseAuditService.WriteDecisionLogAsync(caso, cancellationToken);

        FlashSuccess = $"Caso {caso.ConsecutivoTexto} marcado como coincidente.";
        return RedirectToPage(new { casoId = caso.Id, numeroOficio = NumeroOficioFiltro });
    }

    public async Task<IActionResult> OnPostNoCoincidenteAsync(
        [FromForm(Name = "NoCoincidente.CaseId")] int caseId,
        [FromForm(Name = "NoCoincidente.Observacion")] string? observacion,
        CancellationToken cancellationToken)
    {
        SelectedCaseId = caseId;
        NoCoincidente.CaseId = caseId;
        NoCoincidente.Observacion = observacion?.Trim() ?? string.Empty;

        if (NoCoincidente.CaseId <= 0)
        {
            FlashError = "Selecciona un caso valido para auditoria.";
            return RedirectToPage(new { numeroOficio = NumeroOficioFiltro });
        }

        ModelState.Clear();
        if (!TryValidateModel(NoCoincidente, nameof(NoCoincidente)))
        {
            await LoadPageAsync(cancellationToken);
            return Page();
        }

        var caso = await dbContext.CasosCorreccion
            .Include(x => x.Oficio)
            .Include(x => x.Evidencias)
            .FirstOrDefaultAsync(x => x.Id == NoCoincidente.CaseId, cancellationToken);

        if (caso is null || caso.Oficio is null)
        {
            return NotFound();
        }

        if (!await IsOficioAuditableAsync(caso.OficioId, cancellationToken) ||
            !CatalogosAuditoriaCaso.TieneEvidenciaRevisable(caso.EvidenciaUrl, caso.Evidencias.Count))
        {
            FlashError = "El caso ya no esta disponible para auditoria.";
            return RedirectToPage(new { numeroOficio = NumeroOficioFiltro });
        }

        caseAuditService.MarkNoCoincidente(caso, NoCoincidente.Observacion);
        await dbContext.SaveChangesAsync(cancellationToken);
        await caseAuditService.WriteDecisionLogAsync(caso, cancellationToken);

        FlashSuccess = $"Caso {caso.ConsecutivoTexto} marcado como no coincidente.";
        return RedirectToPage(new { casoId = caso.Id, numeroOficio = NumeroOficioFiltro });
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        NumeroOficioFiltro = NumeroOficioFiltro?.Trim() ?? string.Empty;

        var oficioQuery = dbContext.Oficios
            .AsNoTracking()
            .Where(x =>
                x.Casos.Any() &&
                x.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl != string.Empty)) &&
                x.Casos.Any(c => string.IsNullOrEmpty(c.AuditoriaEstado) || c.AuditoriaEstado == CatalogosAuditoriaCaso.Pendiente));

        TotalOficiosAuditables = await oficioQuery.CountAsync(cancellationToken);

        if (HasNumeroOficioFiltro)
        {
            oficioQuery = oficioQuery.Where(x => EF.Functions.Like(x.NumeroOficio, $"%{NumeroOficioFiltro}%"));
        }

        var oficioProjections = await oficioQuery
            .Select(x => new OficioAuditProjection
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                Casos = x.Casos
                    .OrderBy(c => c.Consecutivo)
                    .Select(c => new CasoAuditProjection
                    {
                        Id = c.Id,
                        Consecutivo = c.Consecutivo,
                        Curp = c.Curp,
                        NombreCompleto = c.NombreCompleto,
                        NivelEducativo = c.NivelEducativo,
                        TipoCorreccion = c.TipoCorreccion,
                        AuditoriaEstado = c.AuditoriaEstado,
                        TotalEvidencias = c.Evidencias.Count,
                        TieneEvidenciaUrl = c.EvidenciaUrl != null && c.EvidenciaUrl != string.Empty
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        Oficios = oficioProjections
            .Select(projection =>
            {
                var casos = projection.Casos
                    .Select(caso => new CasoAuditListItem
                    {
                        Id = caso.Id,
                        Consecutivo = caso.Consecutivo,
                        Curp = caso.Curp,
                        NombreCompleto = caso.NombreCompleto,
                        NivelEducativo = caso.NivelEducativo,
                        TipoCorreccion = caso.TipoCorreccion,
                        AuditoriaEstado = CatalogosAuditoriaCaso.NormalizarEstado(caso.AuditoriaEstado),
                        TotalFuentesEvidencia = caso.TotalEvidencias + (caso.TieneEvidenciaUrl ? 1 : 0)
                    })
                    .ToList();

                var estadoOficio = ResolveOfficeAuditStatus(casos);
                return new OficioAuditGroup
                {
                    Id = projection.Id,
                    NumeroOficio = projection.NumeroOficio,
                    FechaOficio = projection.FechaOficio,
                    Asunto = projection.Asunto,
                    AuditoriaEstado = estadoOficio,
                    Casos = casos
                };
            })
            .OrderBy(x => CatalogosAuditoriaCaso.GetPriority(x.AuditoriaEstado))
            .ThenByDescending(x => x.FechaOficio)
            .ThenBy(x => x.NumeroOficio, StringComparer.Ordinal)
            .ToList();

        var availableCaseIds = Oficios
            .SelectMany(x => x.Casos)
            .Select(x => x.Id)
            .ToHashSet();

        if (SelectedCaseId.HasValue && !availableCaseIds.Contains(SelectedCaseId.Value))
        {
            SelectedCaseId = null;
        }

        if (!SelectedCaseId.HasValue)
        {
            SelectedCaseId = Oficios
                .SelectMany(x => x.Casos)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
        }

        if (SelectedCaseId.HasValue)
        {
            SelectedCase = await LoadSelectedCaseAsync(SelectedCaseId.Value, cancellationToken);
            if (SelectedCase is not null)
            {
                NoCoincidente.CaseId = SelectedCase.Id;
            }
        }

        var selectedCaseIdOrDefault = SelectedCaseId;
        for (var index = 0; index < Oficios.Count; index++)
        {
            var oficio = Oficios[index];
            oficio.IsExpanded = selectedCaseIdOrDefault.HasValue
                ? oficio.Casos.Any(x => x.Id == selectedCaseIdOrDefault.Value)
                : index == 0;
        }

        TotalOficiosPendientes = Oficios.Count;
        VisibleOficios = BuildVisibleOffices(Oficios, SelectedCaseId, OfficePreviewLimit);
    }

    private async Task<SelectedCaseAuditView?> LoadSelectedCaseAsync(int caseId, CancellationToken cancellationToken)
    {
        var selectedCase = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Id == caseId)
            .Select(x => new SelectedCaseAuditView
            {
                Id = x.Id,
                OficioId = x.OficioId,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NombreCompleto = x.NombreCompleto,
                Cct = x.Cct,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                Folio = x.Folio,
                PeriodoInicio = x.PeriodoInicio,
                PeriodoFin = x.PeriodoFin,
                FechaVerificacion = x.FechaVerificacion,
                Observaciones = x.Observaciones,
                Estatus = x.Estatus,
                EvidenciaUrl = x.EvidenciaUrl,
                AuditoriaEstado = CatalogosAuditoriaCaso.NormalizarEstado(x.AuditoriaEstado),
                AuditoriaUltimoResultado = x.AuditoriaUltimoResultado,
                AuditoriaUltimaObservacion = x.AuditoriaUltimaObservacion,
                AuditoriaUltimoUsuario = x.AuditoriaUltimoUsuario,
                AuditoriaUltimaRevisionUtc = x.AuditoriaUltimaRevisionUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (selectedCase is null)
        {
            return null;
        }

        var evidenceRows = await dbContext.EvidenciasFoto
            .AsNoTracking()
            .Where(x => x.CasoCorreccionId == caseId)
            .OrderByDescending(x => x.FechaEvidencia)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.TipoEvidencia,
                x.FechaEvidencia,
                x.Notas
            })
            .ToListAsync(cancellationToken);

        var evidencias = evidenceRows
            .Select(x => new AuditEvidenceItem
            {
                Key = $"file-{x.Id}",
                SourceLabel = "Archivo",
                Title = $"{x.TipoEvidencia} {x.FechaEvidencia:dd/MM/yyyy}",
                Caption = string.IsNullOrWhiteSpace(x.Notas) ? "Sin notas." : x.Notas,
                FullUrl = $"/Evidencias/View/{x.Id}?v={x.Id}",
                ThumbnailUrl = $"/Evidencias/View/{x.Id}?v={x.Id}&thumb=true"
            })
            .ToList();

        selectedCase.Evidencias = evidencias;
        if (!string.IsNullOrWhiteSpace(selectedCase.EvidenciaUrl))
        {
            selectedCase.Evidencias.Add(new AuditEvidenceItem
            {
                Key = "url",
                SourceLabel = "Liga",
                Title = "Evidencia enlazada",
                Caption = "Imagen registrada en EvidenciaUrl.",
                FullUrl = selectedCase.EvidenciaUrl,
                ThumbnailUrl = selectedCase.EvidenciaUrl
            });
        }

        selectedCase.CurrentEvidence = selectedCase.Evidencias
            .FirstOrDefault(x => string.Equals(x.Key, SelectedEvidenceKey, StringComparison.Ordinal))
            ?? selectedCase.Evidencias.FirstOrDefault();

        return selectedCase;
    }

    private async Task<bool> IsOficioAuditableAsync(int oficioId, CancellationToken cancellationToken)
    {
        return await dbContext.Oficios
            .AsNoTracking()
            .Where(x => x.Id == oficioId)
            .AnyAsync(x =>
                x.Casos.Any() &&
                x.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl != string.Empty)),
                cancellationToken);
    }

    private static string ResolveOfficeAuditStatus(IEnumerable<CasoAuditListItem> casos)
    {
        var estados = casos
            .Select(x => CatalogosAuditoriaCaso.NormalizarEstado(x.AuditoriaEstado))
            .ToList();

        if (estados.Any(x => x == CatalogosAuditoriaCaso.Pendiente))
        {
            return CatalogosAuditoriaCaso.Pendiente;
        }

        if (estados.Any(x => x == CatalogosAuditoriaCaso.NoCoincidente))
        {
            return CatalogosAuditoriaCaso.NoCoincidente;
        }

        return CatalogosAuditoriaCaso.Coincidente;
    }

    private static IReadOnlyList<OficioAuditGroup> BuildVisibleOffices(
        IReadOnlyList<OficioAuditGroup> offices,
        int? selectedCaseId,
        int limit)
    {
        if (offices.Count <= limit)
        {
            return offices;
        }

        var visibleOfficeIds = offices
            .Take(limit)
            .Select(x => x.Id)
            .ToHashSet();

        if (selectedCaseId.HasValue)
        {
            var selectedOfficeId = offices
                .Where(x => x.Casos.Any(c => c.Id == selectedCaseId.Value))
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

            if (selectedOfficeId.HasValue && !visibleOfficeIds.Contains(selectedOfficeId.Value))
            {
                visibleOfficeIds.Remove(offices[limit - 1].Id);
                visibleOfficeIds.Add(selectedOfficeId.Value);
            }
        }

        return offices
            .Where(x => visibleOfficeIds.Contains(x.Id))
            .ToList();
    }

    private sealed class OficioAuditProjection
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public string Asunto { get; init; } = string.Empty;
        public List<CasoAuditProjection> Casos { get; init; } = [];
    }

    private sealed class CasoAuditProjection
    {
        public int Id { get; init; }
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string? AuditoriaEstado { get; init; }
        public int TotalEvidencias { get; init; }
        public bool TieneEvidenciaUrl { get; init; }
    }

    public sealed class OficioAuditGroup
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public string Asunto { get; init; } = string.Empty;
        public string AuditoriaEstado { get; init; } = CatalogosAuditoriaCaso.Pendiente;
        public bool IsExpanded { get; set; }
        public List<CasoAuditListItem> Casos { get; init; } = [];
        public string AuditoriaEstadoDisplay => CatalogosAuditoriaCaso.GetDisplayName(AuditoriaEstado);
    }

    public sealed class CasoAuditListItem
    {
        public int Id { get; init; }
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string AuditoriaEstado { get; init; } = CatalogosAuditoriaCaso.Pendiente;
        public int TotalFuentesEvidencia { get; init; }
        public string ConsecutivoTexto => Consecutivo.ToString("000");
        public string AuditoriaEstadoDisplay => CatalogosAuditoriaCaso.GetDisplayName(AuditoriaEstado);
    }

    public sealed class SelectedCaseAuditView
    {
        public int Id { get; init; }
        public int OficioId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string? Cct { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string? Folio { get; init; }
        public int? PeriodoInicio { get; init; }
        public int? PeriodoFin { get; init; }
        public DateTime? FechaVerificacion { get; init; }
        public string? Observaciones { get; init; }
        public string Estatus { get; init; } = string.Empty;
        public string? EvidenciaUrl { get; init; }
        public string AuditoriaEstado { get; init; } = CatalogosAuditoriaCaso.Pendiente;
        public string? AuditoriaUltimoResultado { get; init; }
        public string? AuditoriaUltimaObservacion { get; init; }
        public string? AuditoriaUltimoUsuario { get; init; }
        public DateTime? AuditoriaUltimaRevisionUtc { get; init; }
        public List<AuditEvidenceItem> Evidencias { get; set; } = [];
        public AuditEvidenceItem? CurrentEvidence { get; set; }
        public string ConsecutivoTexto => Consecutivo.ToString("000");
        public string AuditoriaEstadoDisplay => CatalogosAuditoriaCaso.GetDisplayName(AuditoriaEstado);
        public string? UltimoResultadoDisplay => string.IsNullOrWhiteSpace(AuditoriaUltimoResultado)
            ? null
            : CatalogosAuditoriaCaso.GetDisplayName(AuditoriaUltimoResultado);
        public DateTime? AuditoriaUltimaRevisionLocal => AuditoriaUltimaRevisionUtc.HasValue
            ? DateTime.SpecifyKind(AuditoriaUltimaRevisionUtc.Value, DateTimeKind.Utc).ToLocalTime()
            : null;
    }

    public sealed class AuditEvidenceItem
    {
        public string Key { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Caption { get; init; } = string.Empty;
        public string FullUrl { get; init; } = string.Empty;
        public string ThumbnailUrl { get; init; } = string.Empty;
    }

    public sealed class NoCoincidenteInputModel
    {
        public int CaseId { get; set; }

        [Required(ErrorMessage = "Captura el motivo de la revision.")]
        [StringLength(100, ErrorMessage = "El motivo no puede exceder 100 caracteres.")]
        [Display(Name = "Motivo")]
        public string Observacion { get; set; } = string.Empty;
    }
}
