param(
    [string]$TrialRoot = 'C:\BitacoraEvidencias_Pruebas',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]*$')][string]$AppFolder = 'App',
    [string]$ServerAddress = '127.0.0.1',
    [int]$HttpsPort = 5068,
    [switch]$TrustLocalSqlCertificate
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$publish = [IO.Path]::GetFullPath((Join-Path $TrialRoot $AppFolder))
$storage = [IO.Path]::GetFullPath((Join-Path $TrialRoot 'Input\Evidencias'))
$productionRoot = [IO.Path]::GetFullPath('C:\BitacoraEvidencias')
foreach ($path in @($publish,$storage)) {
    if ($path.Equals($productionRoot,[StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($productionRoot + '\',[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Este lanzador es exclusivo del ensayo y no usa rutas productivas.'
    }
}
if ($HttpsPort -eq 5067 -or $HttpsPort -lt 1024 -or $HttpsPort -gt 65535) { throw 'Usa un puerto de ensayo valido distinto de 5067.' }
if (Get-NetTCPConnection -LocalPort $HttpsPort -State Listen -ErrorAction SilentlyContinue) { throw 'El puerto ya esta ocupado. No se detiene ningun proceso.' }
$app = Join-Path $publish 'BitacoraEvidencias.Web.dll'
if (-not (Test-Path -LiteralPath $app) -or -not (Test-Path -LiteralPath $storage)) { throw 'Falta publicar App o preparar las copias del ensayo.' }
foreach ($name in @('BITACORA_DATA_PROTECTION_KEY','BITACORA_HTTPS_CERT_PATH','BITACORA_HTTPS_CERT_PASSWORD')) {
    if (-not [Environment]::GetEnvironmentVariable($name,'Process')) { throw "Falta $name en el entorno de este proceso." }
}
$settings = @{
    ASPNETCORE_ENVIRONMENT = 'Production'
    ASPNETCORE_URLS = "https://${ServerAddress}:$HttpsPort"
    ASPNETCORE_Kestrel__Certificates__Default__Path = $env:BITACORA_HTTPS_CERT_PATH
    ASPNETCORE_Kestrel__Certificates__Default__Password = $env:BITACORA_HTTPS_CERT_PASSWORD
    Storage__RootPath = $storage
    Startup__PrepareApplicationData = 'false'
    Startup__ResetApplicationData = 'false'
    Startup__CheckLegacySchemaOnly = 'false'
    Startup__StampLegacyBaselineOnly = 'false'
    Security__ProtectExistingData = 'false'
}
[string]$user = Read-Host 'Usuario SQL de pruebas'
$password = Read-Host 'Contrasena SQL' -AsSecureString
$credential = [Management.Automation.PSCredential]::new($user,$password)
$connection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$connection.set_DataSource('.\Auditoria')
$connection.set_InitialCatalog('BitacoraEvidencias_Pruebas')
$connection.set_UserID([string]$user)
$connection.set_Password($credential.GetNetworkCredential().Password)
$connection.set_Encrypt($true)
$connection.set_TrustServerCertificate([bool]$TrustLocalSqlCertificate)
$connection.set_PersistSecurityInfo($false)
$settings.ConnectionStrings__DefaultConnection = $connection.ConnectionString
$previous = @{}
try {
    foreach ($name in $settings.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
        [Environment]::SetEnvironmentVariable($name,$settings[$name],'Process')
    }
    Write-Host "Ensayo: https://${ServerAddress}:$HttpsPort/Login ; base BitacoraEvidencias_Pruebas. Ctrl+C para detener."
    Push-Location $publish
    try { & dotnet $app } finally { Pop-Location }
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
    Remove-Variable credential,password,connection,settings -ErrorAction SilentlyContinue
}
