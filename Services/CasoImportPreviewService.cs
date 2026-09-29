using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface ICasoImportPreviewService
{
    Task<ImportCasosModel.CasoImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<ImportCasosModel.CasoImportPreview> BuildPreviewFromRowsAsync(string fileName, IReadOnlyCollection<ImportCasosModel.CasoImportRow> rows, CancellationToken cancellationToken = default);
}

public sealed class CasoImportPreviewService(
    AppDbContext dbContext,
    ITabularImportFileParser tabularImportFileParser) : ICasoImportPreviewService
{
    private static readonly string[] ExpectedHeaders =
    [
        "Oficio",
        "Folio",
        "Cct",
        "PeriodoInicio",
        "PeriodoFin",
        "Curp",
        "NombreCompleto",
        "FechaVerificacion",
        "NIVEL",
        "TipoCorreccion",
        "Observaciones",
        "Estatus",
        "EvidenciaUrl",
        "Validador",
        "AuditorId"
    ];

    public async Task<ImportCasosModel.CasoImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var preview = new ImportCasosModel.CasoImportPreview
        {
            FileName = file.FileName
        };

        var tabularFile = await tabularImportFileParser.ParseAsync(file, cancellationToken);
        foreach (var parserError in tabularFile.Errors)
        {
            preview.Errors.Add(new ImportCasosModel.ImportError
            {
                Message = parserError
            });
        }

        preview.TotalRows = tabularFile.TotalRows;
        if (preview.Errors.Count > 0)
        {
            preview.InvalidRows = preview.TotalRows;
            return preview;
        }

        ValidateExpectedHeaders(tabularFile.Headers, preview.Errors);
        if (preview.Errors.Count > 0)
        {
            preview.InvalidRows = preview.TotalRows;
            return preview;
        }

        if (tabularFile.Rows.Count == 0)
        {
            preview.Errors.Add(new ImportCasosModel.ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return preview;
        }

        var candidates = tabularFile.Rows
            .Select(row => new CasoImportCandidate
            {
                SourceRowNumber = row.SourceRowNumber,
                OficioNumero = GetColumnValue(row, 0),
                Folio = GetColumnValue(row, 1),
                Cct = GetColumnValue(row, 2),
                PeriodoInicioRaw = GetColumnValue(row, 3),
                PeriodoFinRaw = GetColumnValue(row, 4),
                Curp = GetColumnValue(row, 5),
                NombreCompleto = GetColumnValue(row, 6),
                FechaVerificacionRaw = GetColumnValue(row, 7),
                NivelRaw = GetColumnValue(row, 8),
                TipoCorreccion = GetColumnValue(row, 9),
                Observaciones = GetColumnValue(row, 10),
                EstatusRaw = GetColumnValue(row, 11),
                EvidenciaUrlRaw = GetColumnValue(row, 12),
                Validador = GetColumnValue(row, 13),
                AuditorIdRaw = GetColumnValue(row, 14)
            })
            .ToList();

        await PopulatePreviewAsync(preview, candidates, cancellationToken);
        return preview;
    }

    public async Task<ImportCasosModel.CasoImportPreview> BuildPreviewFromRowsAsync(
        string fileName,
        IReadOnlyCollection<ImportCasosModel.CasoImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var preview = new ImportCasosModel.CasoImportPreview
        {
            FileName = fileName,
            TotalRows = rows.Count
        };

        var candidates = rows
            .OrderBy(row => row.SourceRowNumber)
            .Select(row => new CasoImportCandidate
            {
                SourceRowNumber = row.SourceRowNumber,
                OficioNumero = row.OficioNumero,
                Folio = row.Folio,
                Cct = row.Cct,
                PeriodoInicioRaw = row.PeriodoInicio.ToString(),
                PeriodoFinRaw = row.PeriodoFin.ToString(),
                Curp = row.Curp,
                NombreCompleto = row.NombreCompleto,
                FechaVerificacionRaw = row.FechaVerificacion.ToString("dd/MM/yyyy"),
                NivelRaw = row.NivelEducativo,
                TipoCorreccion = row.TipoCorreccion,
                Observaciones = row.Observaciones,
                EstatusRaw = row.Estatus,
                EvidenciaUrlRaw = row.EvidenciaUrl,
                Validador = row.Validador,
                AuditorIdRaw = row.AuditorUsuarioId.ToString()
            })
            .ToList();

        await PopulatePreviewAsync(preview, candidates, cancellationToken);
        return preview;
    }

    private async Task PopulatePreviewAsync(
        ImportCasosModel.CasoImportPreview preview,
        IReadOnlyList<CasoImportCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            preview.Errors.Add(new ImportCasosModel.ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return;
        }

        var officeTargets = await LoadOfficeTargetsAsync(
            candidates
                .Select(candidate => candidate.OficioNumero?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            cancellationToken);
        var activeAuditors = await LoadActiveAuditorsAsync(cancellationToken);

        var seenOfficeLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var rowErrors = new List<string>();

            var oficioNumero = candidate.OficioNumero?.Trim() ?? string.Empty;
            var folio = ImportParsing.NormalizeOptional(candidate.Folio);
            var cct = candidate.Cct?.Trim() ?? string.Empty;
            var curp = candidate.Curp?.Trim() ?? string.Empty;
            var nombreCompleto = candidate.NombreCompleto?.Trim() ?? string.Empty;
            var tipoCorreccion = candidate.TipoCorreccion?.Trim() ?? string.Empty;
            var observaciones = ImportParsing.NormalizeOptional(candidate.Observaciones);
            var validador = ImportParsing.NormalizeOptional(candidate.Validador);

            OfficeImportTarget? officeTarget = null;
            if (string.IsNullOrWhiteSpace(oficioNumero))
            {
                rowErrors.Add("Oficio es obligatorio.");
            }
            else if (!officeTargets.TryGetValue(oficioNumero, out officeTarget))
            {
                rowErrors.Add("El oficio no existe en la base.");
            }

            if (!string.IsNullOrWhiteSpace(folio) && folio.Length > 80)
            {
                rowErrors.Add("Folio excede 80 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(cct))
            {
                rowErrors.Add("Cct es obligatorio.");
            }
            else if (cct.Length > 30)
            {
                rowErrors.Add("Cct excede 30 caracteres.");
            }

            var hasPeriodoInicio = ImportParsing.TryParseRequiredYear(candidate.PeriodoInicioRaw, out var periodoInicio);
            if (!hasPeriodoInicio)
            {
                rowErrors.Add("PeriodoInicio debe ser un ano valido entre 1900 y 2100.");
            }

            var hasPeriodoFin = ImportParsing.TryParseRequiredYear(candidate.PeriodoFinRaw, out var periodoFin);
            if (!hasPeriodoFin)
            {
                rowErrors.Add("PeriodoFin debe ser un ano valido entre 1900 y 2100.");
            }
            else if (hasPeriodoInicio && periodoFin < periodoInicio)
            {
                rowErrors.Add("PeriodoFin no puede ser menor a PeriodoInicio.");
            }

            if (string.IsNullOrWhiteSpace(curp))
            {
                rowErrors.Add("Curp es obligatorio.");
            }
            else if (curp.Length > 18)
            {
                rowErrors.Add("Curp excede 18 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(nombreCompleto))
            {
                rowErrors.Add("NombreCompleto es obligatorio.");
            }
            else if (nombreCompleto.Length > 140)
            {
                rowErrors.Add("NombreCompleto excede 140 caracteres.");
            }

            if (!ImportParsing.TryParseRequiredDate(candidate.FechaVerificacionRaw, out var fechaVerificacion))
            {
                rowErrors.Add("FechaVerificacion no tiene un formato valido. Usa dd/MM/yyyy.");
            }

            var nivelEducativo = NormalizeCatalogValue(candidate.NivelRaw, CatalogosCaso.NivelesEducativos);
            if (nivelEducativo is null)
            {
                rowErrors.Add("NIVEL debe ser Primaria o Secundaria.");
            }

            if (string.IsNullOrWhiteSpace(tipoCorreccion))
            {
                rowErrors.Add("TipoCorreccion es obligatorio.");
            }
            else if (tipoCorreccion.Length > 80)
            {
                rowErrors.Add("TipoCorreccion excede 80 caracteres.");
            }

            var estatus = NormalizeCatalogValue(candidate.EstatusRaw, CatalogosCaso.Estatus);
            if (estatus is null)
            {
                rowErrors.Add("Estatus no es valido.");
            }

            if (!ImportParsing.TryParseOptionalHttpUrl(candidate.EvidenciaUrlRaw, out var evidenciaUrl))
            {
                rowErrors.Add("EvidenciaUrl debe ser una URL http/https valida.");
            }

            if (!string.IsNullOrWhiteSpace(validador) && validador.Length > 140)
            {
                rowErrors.Add("Validador excede 140 caracteres.");
            }

            int? auditorUsuarioId = null;
            if (string.IsNullOrWhiteSpace(candidate.AuditorIdRaw))
            {
                rowErrors.Add("AuditorId es obligatorio.");
            }
            else if (!int.TryParse(candidate.AuditorIdRaw.Trim(), out var parsedAuditorId) || parsedAuditorId <= 0)
            {
                rowErrors.Add("AuditorId debe ser un entero positivo.");
            }
            else if (!activeAuditors.ContainsKey(parsedAuditorId))
            {
                rowErrors.Add("AuditorId no corresponde a un usuario activo.");
            }
            else
            {
                auditorUsuarioId = parsedAuditorId;
            }

            if (officeTarget is not null && nivelEducativo is not null)
            {
                if (officeTarget.ExistingLevels.Contains(nivelEducativo))
                {
                    rowErrors.Add($"El oficio ya tiene un caso para NIVEL '{nivelEducativo}'.");
                }

                var officeLevelKey = BuildOfficeLevelKey(oficioNumero, nivelEducativo);
                if (seenOfficeLevels.TryGetValue(officeLevelKey, out var conflictingRowNumber))
                {
                    rowErrors.Add($"Ya existe otro caso para ese OFICIO y NIVEL dentro del archivo. Tambien aparece en la fila {conflictingRowNumber}.");
                }
                else
                {
                    seenOfficeLevels[officeLevelKey] = candidate.SourceRowNumber;
                }
            }

            if (rowErrors.Count > 0)
            {
                foreach (var error in rowErrors)
                {
                    preview.Errors.Add(new ImportCasosModel.ImportError
                    {
                        RowNumber = candidate.SourceRowNumber,
                        Message = error
                    });
                }

                continue;
            }

            preview.Rows.Add(new ImportCasosModel.CasoImportRow
            {
                SourceRowNumber = candidate.SourceRowNumber,
                OficioNumero = oficioNumero,
                Folio = folio,
                Cct = cct,
                PeriodoInicio = periodoInicio,
                PeriodoFin = periodoFin,
                Curp = curp,
                NombreCompleto = nombreCompleto,
                FechaVerificacion = fechaVerificacion.Date,
                NivelEducativo = nivelEducativo!,
                TipoCorreccion = tipoCorreccion,
                Observaciones = observaciones,
                Estatus = estatus!,
                EvidenciaUrl = evidenciaUrl,
                Validador = validador,
                AuditorUsuarioId = auditorUsuarioId!.Value
            });

            officeTarget!.NextConsecutivo++;
            officeTarget!.ExistingLevels.Add(nivelEducativo!);
        }

        preview.InvalidRows = preview.TotalRows - preview.Rows.Count;
    }

    private async Task<Dictionary<string, OfficeImportTarget>> LoadOfficeTargetsAsync(
        IReadOnlyCollection<string> officeNumbers,
        CancellationToken cancellationToken)
    {
        if (officeNumbers.Count == 0)
        {
            return new Dictionary<string, OfficeImportTarget>(StringComparer.Ordinal);
        }

        var offices = await dbContext.Oficios
            .AsNoTracking()
            .Where(x => officeNumbers.Contains(x.NumeroOficio))
            .Select(x => new
            {
                x.Id,
                x.NumeroOficio,
                ExistingLevels = x.Casos.Select(caso => caso.NivelEducativo).ToList(),
                NextConsecutivo = x.Casos.Count + 1
            })
            .ToListAsync(cancellationToken);

        return offices.ToDictionary(
            item => item.NumeroOficio,
            item => new OfficeImportTarget
            {
                OficioId = item.Id,
                NextConsecutivo = item.NextConsecutivo,
                ExistingLevels = new HashSet<string>(item.ExistingLevels, StringComparer.Ordinal)
            },
            StringComparer.Ordinal);
    }

    private async Task<Dictionary<int, string>> LoadActiveAuditorsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.UsuariosSistema
            .AsNoTracking()
            .Where(x => x.Activo)
            .ToDictionaryAsync(x => x.Id, x => x.Usuario, cancellationToken);
    }

    private static void ValidateExpectedHeaders(IReadOnlyList<string> headers, ICollection<ImportCasosModel.ImportError> errors)
    {
        ImportPreviewSupport.ValidateExactHeaders(
            headers,
            ExpectedHeaders,
            errors,
            message => new ImportCasosModel.ImportError { Message = message });
    }

    private static string? GetColumnValue(TabularImportRow row, int index)
        => ImportPreviewSupport.GetColumnValue(row, index);

    private static string? NormalizeCatalogValue(string? rawValue, IReadOnlyList<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        return allowedValues.FirstOrDefault(value => string.Equals(value, trimmed, StringComparison.Ordinal));
    }

    private static string BuildOfficeLevelKey(string oficioNumero, string nivelEducativo)
        => $"{oficioNumero}::{nivelEducativo}";

    private sealed class CasoImportCandidate
    {
        public int SourceRowNumber { get; init; }
        public string? OficioNumero { get; init; }
        public string? Folio { get; init; }
        public string? Cct { get; init; }
        public string? PeriodoInicioRaw { get; init; }
        public string? PeriodoFinRaw { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string? FechaVerificacionRaw { get; init; }
        public string? NivelRaw { get; init; }
        public string? TipoCorreccion { get; init; }
        public string? Observaciones { get; init; }
        public string? EstatusRaw { get; init; }
        public string? EvidenciaUrlRaw { get; init; }
        public string? Validador { get; init; }
        public string? AuditorIdRaw { get; init; }
    }

    private sealed class OfficeImportTarget
    {
        public int OficioId { get; init; }
        public int NextConsecutivo { get; set; }
        public HashSet<string> ExistingLevels { get; init; } = new(StringComparer.Ordinal);
    }
}
