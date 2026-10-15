using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Pages;
using BitacoraEvidencias.Web.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Migration;

// Executes the real, unfiltered search handler. No data, SQL text, raw plans or
// parameter values leave this process. STATISTICS XML changes session state only.
public static class SearchDiagnostics
{
    public static async Task RunAsync(string connectionString)
    {
        var capture = new Capture();
        var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions()));
        await using var context = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(capture).Options, protection);
        await context.Database.OpenConnectionAsync();
        Console.WriteLine("Diagnostico de Busqueda sin filtros, pagina 1, 20 filas. Solo lectura; no se muestran registros.");
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Console.WriteLine($"Indices PagingIndexes registrados: {applied.Any(id => id.EndsWith("_PagingIndexes"))}.");
        CapturedCommand[] queries = [];
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            capture.Commands.Clear();
            var page = new BusquedaModel(context, protection);
            var timer = Stopwatch.StartNew();
            await page.OnGetAsync();
            timer.Stop();
            queries = capture.Commands.ToArray();
            Console.WriteLine($"Intento {attempt}: handler {timer.ElapsedMilliseconds} ms; total {page.TotalRows}; visibles {page.Resultados.Count}.");
            for (var index = 0; index < queries.Length; index++)
                Console.WriteLine($"  Consulta {index + 1} ({Label(index)}): {queries[index].Milliseconds} ms.");
        }
        for (var index = 0; index < queries.Length; index++)
        {
            Console.WriteLine($"Plan real de consulta {index + 1} ({Label(index)}):");
            try
            {
                await ReadPlanAsync((SqlConnection)context.Database.GetDbConnection(), queries[index]);
            }
            catch (SqlException exception)
            {
                Console.WriteLine($"  Plan no disponible: codigo SQL {exception.Number}, estado {exception.State}. Para obtener el plan se requiere permiso SHOWPLAN.");
            }
        }
        Console.WriteLine("Diagnostico terminado. No ejecutar import ni modificar produccion para esta prueba.");
    }

    private static string Label(int index) => index switch { 0 => "conteo", 1 => "identificadores de pagina", _ => "datos de pagina" };

    private static async Task ReadPlanAsync(SqlConnection connection, CapturedCommand query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SET STATISTICS XML ON;\n" + query.Text + ";\nSET STATISTICS XML OFF;";
        command.CommandTimeout = 60;
        foreach (var parameter in query.Parameters)
            command.Parameters.Add((SqlParameter)((ICloneable)parameter).Clone());
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            do
            {
                var isPlan = reader.FieldCount == 1 && (reader.GetDataTypeName(0) == "xml"
                    || reader.GetName(0).Contains("Showplan", StringComparison.OrdinalIgnoreCase));
                while (await reader.ReadAsync())
                {
                    if (!isPlan) continue; // Consume rows without displaying them.
                    foreach (var line in SummarizePlan(reader.GetString(0))) Console.WriteLine("  " + line);
                }
            } while (await reader.NextResultAsync());
        }
        finally
        {
            using var reset = connection.CreateCommand();
            reset.CommandText = "SET STATISTICS XML OFF;";
            await reset.ExecuteNonQueryAsync();
        }
    }

    // Explicit allowlist: plans contain SQL, parameter values and potentially
    // personal data. Only numeric metrics and validated wait identifiers are emitted.
    public static IReadOnlyList<string> SummarizePlan(string xml)
    {
        var document = XDocument.Parse(xml);
        XNamespace ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
        var lines = new List<string>();
        foreach (var plan in document.Descendants(ns + "QueryPlan"))
            lines.Add($"Compilacion: tiempo {Number(plan, "CompileTime")} ms; CPU {Number(plan, "CompileCPU")} ms; memoria {Number(plan, "CompileMemory")} KB.");
        foreach (var time in document.Descendants(ns + "QueryTimeStats"))
            lines.Add($"Tiempo plan: transcurrido {Number(time, "ElapsedTime")} ms; CPU {Number(time, "CpuTime")} ms.");
        foreach (var grant in document.Descendants(ns + "MemoryGrantInfo"))
            lines.Add($"Memoria: espera {Number(grant, "GrantWaitTime")} ms; solicitada {Number(grant, "RequestedMemory")} KB; concedida {Number(grant, "GrantedMemory")} KB; usada {Number(grant, "MaxUsedMemory")} KB.");
        foreach (var wait in document.Descendants(ns + "Wait"))
        {
            var type = (string?)wait.Attribute("WaitType") ?? "";
            if (Regex.IsMatch(type, "\\A[A-Z][A-Z0-9_]{0,79}\\z", RegexOptions.CultureInvariant))
                lines.Add($"Espera {type}: {Number(wait, "WaitTimeMs")} ms; veces {Number(wait, "WaitCount")}.");
        }
        lines.Add($"Ordenamientos: {document.Descendants(ns + "Sort").Count()}; avisos de derrame a disco: {document.Descendants(ns + "SpillToTempDb").Count()}.");
        return lines;
    }

    private static string Number(XElement element, string name) =>
        double.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) && value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : "n/d";

    private sealed record CapturedCommand(string Text, SqlParameter[] Parameters, long Milliseconds);

    private sealed class Capture : DbCommandInterceptor
    {
        public List<CapturedCommand> Commands { get; } = [];

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Commands.Add(new(command.CommandText, command.Parameters.Cast<SqlParameter>()
                .Select(p => (SqlParameter)((ICloneable)p).Clone()).ToArray(), (long)eventData.Duration.TotalMilliseconds));
            return ValueTask.FromResult(result);
        }
    }
}
