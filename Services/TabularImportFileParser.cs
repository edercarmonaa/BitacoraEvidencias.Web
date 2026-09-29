using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace BitacoraEvidencias.Web.Services;

public class TabularImportFileParser : ITabularImportFileParser
{
    private const int MaxDataRows = 1000;
    private const long MaxImportFileBytes = 10 * 1024 * 1024;

    private static readonly HashSet<int> BuiltInDateNumberFormats =
    [
        14, 15, 16, 17, 18, 19, 20, 21, 22, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58
    ];

    public async Task<TabularImportFile> ParseAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var extension = Path.GetExtension(file.FileName)?.Trim().ToLowerInvariant();
        if (file.Length > MaxImportFileBytes)
        {
            return new TabularImportFile
            {
                FileName = file.FileName,
                Format = extension ?? string.Empty,
                Errors = { $"El archivo excede el limite permitido de {MaxImportFileBytes / (1024 * 1024)} MB." }
            };
        }

        return extension switch
        {
            ".csv" => await ParseCsvAsync(file, cancellationToken),
            ".xlsx" => await ParseXlsxAsync(file, cancellationToken),
            _ => new TabularImportFile
            {
                FileName = file.FileName,
                Format = extension ?? string.Empty,
                Errors = { "Solo se permiten archivos .csv o .xlsx." }
            }
        };
    }

    private static async Task<TabularImportFile> ParseCsvAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var result = new TabularImportFile
        {
            FileName = file.FileName,
            Format = "csv"
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            result.Errors.Add("El archivo CSV esta vacio.");
            return result;
        }

        var delimiter = DetectDelimiter(content);
        var records = ParseCsvRecords(content, delimiter);
        if (records.Count == 0)
        {
            result.Errors.Add("No se encontraron filas legibles en el archivo CSV.");
            return result;
        }

        var headerRecord = records[0];
        foreach (var value in headerRecord.Values)
        {
            result.Headers.Add(value.Trim());
        }

        var dataRowCount = 0;
        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Values.All(value => string.IsNullOrWhiteSpace(value)))
            {
                continue;
            }

            dataRowCount++;
            if (!TryAcceptDataRow(result, dataRowCount))
            {
                return result;
            }

            var row = new TabularImportRow
            {
                SourceRowNumber = record.SourceLineNumber
            };

            foreach (var value in record.Values)
            {
                row.Values.Add(value.Trim());
            }

            result.Rows.Add(row);
        }

        result.TotalRows = dataRowCount;
        if (result.Headers.Count == 0)
        {
            result.Errors.Add("No se encontro la fila de encabezados en el archivo CSV.");
        }

        return result;
    }

    private static async Task<TabularImportFile> ParseXlsxAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var fileStream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var result = new TabularImportFile
        {
            FileName = file.FileName,
            Format = "xlsx"
        };

        try
        {
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);
            var worksheetPath = ResolveFirstWorksheetPath(archive);
            if (string.IsNullOrWhiteSpace(worksheetPath))
            {
                result.Errors.Add("No se encontro ninguna hoja legible en el archivo Excel.");
                return result;
            }

            var sharedStrings = ReadSharedStrings(archive);
            var dateStyleIndexes = ReadDateStyleIndexes(archive);
            var worksheetEntry = archive.GetEntry(worksheetPath);
            if (worksheetEntry is null)
            {
                result.Errors.Add("No fue posible abrir la primera hoja del archivo Excel.");
                return result;
            }

            var spreadsheetNamespace = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            using var worksheetStream = worksheetEntry.Open();
            var worksheet = XDocument.Load(worksheetStream);
            var rows = worksheet.Root?
                .Element(spreadsheetNamespace + "sheetData")?
                .Elements(spreadsheetNamespace + "row")
                .ToList() ?? [];

            var headerLoaded = false;
            var dataRowCount = 0;
            foreach (var rowElement in rows)
            {
                var rowNumber = (int?)rowElement.Attribute("r") ?? 0;
                var valuesByColumn = new SortedDictionary<int, string>();
                foreach (var cell in rowElement.Elements(spreadsheetNamespace + "c"))
                {
                    var cellReference = cell.Attribute("r")?.Value ?? string.Empty;
                    var columnIndex = GetColumnIndex(cellReference);
                    valuesByColumn[columnIndex] = ReadCellValue(cell, sharedStrings, dateStyleIndexes, spreadsheetNamespace);
                }

                if (valuesByColumn.Count == 0)
                {
                    continue;
                }

                var maxColumn = valuesByColumn.Keys.Max();
                var rowValues = new List<string>(capacity: maxColumn + 1);
                for (var index = 0; index <= maxColumn; index++)
                {
                    rowValues.Add(valuesByColumn.TryGetValue(index, out var value) ? value.Trim() : string.Empty);
                }

                if (!headerLoaded)
                {
                    foreach (var value in rowValues)
                    {
                        result.Headers.Add(value);
                    }

                    headerLoaded = true;
                    continue;
                }

                if (rowValues.All(value => string.IsNullOrWhiteSpace(value)))
                {
                    continue;
                }

                dataRowCount++;
                if (!TryAcceptDataRow(result, dataRowCount))
                {
                    return result;
                }

                var row = new TabularImportRow
                {
                    SourceRowNumber = rowNumber
                };

                foreach (var value in rowValues)
                {
                    row.Values.Add(value);
                }

                result.Rows.Add(row);
            }

            result.TotalRows = dataRowCount;
            if (!headerLoaded)
            {
                result.Errors.Add("No se encontro la fila de encabezados en el archivo Excel.");
            }
        }
        catch (InvalidDataException)
        {
            result.Errors.Add("El archivo Excel no tiene un formato .xlsx valido.");
        }
        catch
        {
            result.Errors.Add("No fue posible leer el archivo Excel.");
        }

        return result;
    }

    private static char DetectDelimiter(string content)
    {
        var firstLine = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        var candidates = new Dictionary<char, int>
        {
            [','] = CountDelimiterOccurrences(firstLine, ','),
            [';'] = CountDelimiterOccurrences(firstLine, ';'),
            ['\t'] = CountDelimiterOccurrences(firstLine, '\t')
        };

        return candidates
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key == ';' ? 0 : 1)
            .First().Key;
    }

    private static int CountDelimiterOccurrences(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        foreach (var character in line)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && character == delimiter)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryAcceptDataRow(TabularImportFile result, int dataRowCount)
    {
        if (dataRowCount <= MaxDataRows)
        {
            return true;
        }

        result.TotalRows = dataRowCount;
        result.Rows.Clear();
        result.Errors.Add($"El archivo excede el maximo permitido de {MaxDataRows} filas de datos.");
        return false;
    }

    private static List<CsvRecord> ParseCsvRecords(string content, char delimiter)
    {
        var records = new List<CsvRecord>();
        var currentRow = new List<string>();
        var currentValue = new StringBuilder();
        var inQuotes = false;
        var recordStartLine = 1;
        var currentLine = 1;

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    currentValue.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && character == delimiter)
            {
                currentRow.Add(currentValue.ToString());
                currentValue.Clear();
                continue;
            }

            if (!inQuotes && (character == '\r' || character == '\n'))
            {
                currentRow.Add(currentValue.ToString());
                currentValue.Clear();
                records.Add(new CsvRecord(recordStartLine, [.. currentRow]));
                currentRow.Clear();

                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                currentLine++;
                recordStartLine = currentLine;
                continue;
            }

            currentValue.Append(character);
            if (character == '\n')
            {
                currentLine++;
            }
        }

        if (currentValue.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentValue.ToString());
            records.Add(new CsvRecord(recordStartLine, [.. currentRow]));
        }

        return records;
    }

    private static string? ResolveFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relationshipEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relationshipEntry is null)
        {
            return null;
        }

        var workbookNamespace = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var relationshipNamespace = (XNamespace)"http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var packageRelationshipNamespace = (XNamespace)"http://schemas.openxmlformats.org/package/2006/relationships";

        using var workbookStream = workbookEntry.Open();
        using var relationshipStream = relationshipEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var relationships = XDocument.Load(relationshipStream);

        var firstSheet = workbook.Root?
            .Element(workbookNamespace + "sheets")?
            .Elements(workbookNamespace + "sheet")
            .FirstOrDefault();

        var relationshipId = firstSheet?.Attribute(relationshipNamespace + "id")?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        var target = relationships.Root?
            .Elements(packageRelationshipNamespace + "Relationship")
            .FirstOrDefault(item => string.Equals(item.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal))?
            .Attribute("Target")?
            .Value;

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        return target.StartsWith('/')
            ? target.TrimStart('/')
            : $"xl/{target.TrimStart('/')}";
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        var spreadsheetNamespace = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root?
            .Elements(spreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(spreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList() ?? [];
    }

    private static HashSet<int> ReadDateStyleIndexes(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/styles.xml");
        if (entry is null)
        {
            return [];
        }

        var spreadsheetNamespace = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var stream = entry.Open();
        var document = XDocument.Load(stream);

        var customFormats = document.Root?
            .Element(spreadsheetNamespace + "numFmts")?
            .Elements(spreadsheetNamespace + "numFmt")
            .Where(item => item.Attribute("numFmtId") is not null && item.Attribute("formatCode") is not null)
            .ToDictionary(
                item => int.Parse(item.Attribute("numFmtId")!.Value, CultureInfo.InvariantCulture),
                item => item.Attribute("formatCode")!.Value,
                EqualityComparer<int>.Default) ?? new Dictionary<int, string>();

        var dateStyles = new HashSet<int>();
        var cellFormats = document.Root?
            .Element(spreadsheetNamespace + "cellXfs")?
            .Elements(spreadsheetNamespace + "xf")
            .ToList() ?? [];

        for (var index = 0; index < cellFormats.Count; index++)
        {
            var xf = cellFormats[index];
            var numFmtId = (int?)xf.Attribute("numFmtId") ?? 0;
            customFormats.TryGetValue(numFmtId, out var formatCode);
            if (IsDateNumberFormat(numFmtId, formatCode))
            {
                dateStyles.Add(index);
            }
        }

        return dateStyles;
    }

    private static bool IsDateNumberFormat(int numFmtId, string? formatCode)
    {
        if (BuiltInDateNumberFormats.Contains(numFmtId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(formatCode))
        {
            return false;
        }

        var normalized = formatCode.Replace("\\", string.Empty).Replace("\"", string.Empty).ToLowerInvariant();
        return normalized.Contains('y') && normalized.Contains('d');
    }

    private static int GetColumnIndex(string cellReference)
    {
        var column = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            column *= 26;
            column += char.ToUpperInvariant(character) - 'A' + 1;
        }

        return Math.Max(column - 1, 0);
    }

    private static string ReadCellValue(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyleIndexes,
        XNamespace spreadsheetNamespace)
    {
        var cellType = cell.Attribute("t")?.Value;
        var styleIndex = (int?)cell.Attribute("s");

        if (string.Equals(cellType, "inlineStr", StringComparison.Ordinal))
        {
            return string.Concat(cell.Descendants(spreadsheetNamespace + "t").Select(text => text.Value));
        }

        var rawValue = cell.Element(spreadsheetNamespace + "v")?.Value ?? string.Empty;
        if (string.Equals(cellType, "s", StringComparison.Ordinal) &&
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex) &&
            sharedStringIndex >= 0 &&
            sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        if (string.Equals(cellType, "b", StringComparison.Ordinal))
        {
            return rawValue == "1" ? "TRUE" : "FALSE";
        }

        if (styleIndex.HasValue &&
            dateStyleIndexes.Contains(styleIndex.Value) &&
            double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            try
            {
                return DateTime.FromOADate(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return rawValue;
            }
        }

        return rawValue;
    }

    private sealed record CsvRecord(int SourceLineNumber, List<string> Values);
}
