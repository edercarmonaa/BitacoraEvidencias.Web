using System.Collections;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace BitacoraEvidencias.Web.Services;

// No HTTP endpoint. Only the local Windows identity owning this process can
// request a graceful stop and obtain the in-memory settings needed to restart it.
public sealed class LocalBackupControlService(IHostEnvironment environment, IConfiguration configuration,
    IHostApplicationLifetime lifetime, ILogger<LocalBackupControlService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows()) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream($"BitacoraBackup_{Environment.ProcessId}", PipeDirection.InOut,
                    1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stoppingToken);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
                if (await reader.ReadLineAsync(timeout.Token) != "PREPARE_BACKUP_V1") continue;
                var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"));
                string[] localServers = [@".\Auditoria", @"localhost\Auditoria", @"127.0.0.1\Auditoria", @"(local)\Auditoria", Environment.MachineName + @"\Auditoria"];
                if (!localServers.Contains(connection.DataSource, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException("La instancia SQL no corresponde al respaldo local.");
                var variables = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                    .ToDictionary(item => (string)item.Key, item => item.Value?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);
                var snapshot = BackupRuntimeSnapshot.Create(Environment.ProcessId, Environment.ProcessPath!,
                    Assembly.GetEntryAssembly()!.Location, environment.ContentRootPath, connection.InitialCatalog, variables,
                    configuration["Storage:RootPath"] ?? "Storage");
                await writer.WriteLineAsync(JsonSerializer.Serialize(snapshot).AsMemory(), timeout.Token);
                if (await reader.ReadLineAsync(timeout.Token) != "STOP_FOR_BACKUP_V1") continue;
                logger.LogInformation("Respaldo local: cierre ordenado solicitado por la cuenta Windows de la aplicacion.");
                lifetime.StopApplication();
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (OperationCanceledException) { /* Incomplete local request: leave application running. */ }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                logger.LogWarning("Control local de respaldo no disponible temporalmente; no se imprimen detalles ni configuracion.");
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}

public sealed record BackupRuntimeSnapshot(int Version, int ProcessId, string Executable, string[] Arguments,
    string ContentRoot, string DatabaseName, string EvidenceRoot, IReadOnlyDictionary<string, string> Environment)
{
    public static BackupRuntimeSnapshot Create(int processId, string executable, string assembly, string contentRoot,
        string databaseName, IReadOnlyDictionary<string, string> environment, string storageRoot = "Storage")
    {
        var name = Path.GetFileName(executable).ToLowerInvariant();
        if (name is not ("dotnet.exe" or "dotnet" or "bitacoraevidencias.web.exe") ||
            Path.GetFileName(assembly) != "BitacoraEvidencias.Web.dll")
            throw new InvalidOperationException("El control de respaldo requiere el host independiente de la aplicacion.");
        if (databaseName is not ("BitacoraEvidencias" or "BitacoraEvidencias_Pruebas"))
            throw new InvalidOperationException("Base de aplicacion desconocida.");
        string[] prefixes = ["ASPNETCORE_", "DOTNET_", "ConnectionStrings__", "Storage__", "Security__", "Startup__", "Logging__", "AuditArchive__", "Reports__", "BITACORA_"];
        var filtered = environment.Where(pair => prefixes.Any(prefix => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var evidenceRoot = Path.GetFullPath(Path.IsPathRooted(storageRoot) ? storageRoot : Path.Combine(contentRoot, storageRoot));
        return new(1, processId, executable, name.StartsWith("dotnet") ? [assembly] : [], contentRoot, databaseName, evidenceRoot, filtered);
    }
}
