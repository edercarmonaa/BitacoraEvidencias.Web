using BitacoraEvidencias.Web.Services;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Services;

public class BackupRuntimeSnapshotTests
{
    [Fact]
    public void RestartSnapshotPreservesApplicationSettingsAndExcludesUnrelatedSecrets()
    {
        var environment = new Dictionary<string,string>
        {
            ["ConnectionStrings__DefaultConnection"] = "fake;password=quoted;value",
            ["BITACORA_DATA_PROTECTION_KEY"] = "fake-key", ["ASPNETCORE_URLS"] = "https://127.0.0.1:5068",
            ["BITACORA_HTTPS_CERT_PASSWORD"] = "fake-cert-password", ["Storage__RootPath"] = @"C:\evidence",
            ["UNRELATED_API_TOKEN"] = "must-not-be-exported"
        };
        var snapshot = BackupRuntimeSnapshot.Create(123, @"C:\dotnet\dotnet.exe", @"C:\app\BitacoraEvidencias.Web.dll", @"C:\app", "BitacoraEvidencias_Pruebas", environment);
        Assert.Equal(123, snapshot.ProcessId);
        Assert.Equal(@"C:\app\BitacoraEvidencias.Web.dll", Assert.Single(snapshot.Arguments));
        Assert.Equal(environment["BITACORA_DATA_PROTECTION_KEY"], snapshot.Environment["BITACORA_DATA_PROTECTION_KEY"]);
        Assert.Equal(environment["ConnectionStrings__DefaultConnection"], snapshot.Environment["ConnectionStrings__DefaultConnection"]);
        Assert.False(snapshot.Environment.ContainsKey("UNRELATED_API_TOKEN"));
    }

    [Fact]
    public void ApplicationHostRestartsWithoutExtraArguments()
    {
        var snapshot = BackupRuntimeSnapshot.Create(123, @"C:\app\BitacoraEvidencias.Web.exe", @"C:\app\BitacoraEvidencias.Web.dll", @"C:\app", "BitacoraEvidencias", new Dictionary<string,string>());
        Assert.Empty(snapshot.Arguments);
        Assert.Equal(@"C:\app\Storage", snapshot.EvidenceRoot);
    }

    [Fact]
    public void SnapshotUsesEffectiveConfiguredEvidenceDirectory()
    {
        var snapshot = BackupRuntimeSnapshot.Create(123, @"C:\dotnet\dotnet.exe", @"C:\app\BitacoraEvidencias.Web.dll",
            @"C:\app", "BitacoraEvidencias", new Dictionary<string,string>(), @"D:\evidence");
        Assert.Equal(@"D:\evidence", snapshot.EvidenceRoot);
    }

    [Theory]
    [InlineData("other.exe", "BitacoraEvidencias.Web.dll", "BitacoraEvidencias")]
    [InlineData("dotnet.exe", "other.dll", "BitacoraEvidencias")]
    [InlineData("dotnet.exe", "BitacoraEvidencias.Web.dll", "other")]
    public void UnknownHostsAndDatabasesCannotBeRestarted(string executable, string assembly, string database) =>
        Assert.Throws<InvalidOperationException>(() => BackupRuntimeSnapshot.Create(123, executable, assembly, "app", database, new Dictionary<string,string>()));
}
