param(
    [Parameter(Mandatory=$true)][string]$ApplicationFolder,
    [string]$EvidenceRoot='C:\BitacoraEvidencias\Evidencias',
    [string]$ServerAddress='127.0.0.1',
    [ValidateRange(1024,65535)][int]$HttpsPort=5067,
    [string]$SqlCredentialFile=$env:BITACORA_APPLICATION_SQL_CREDENTIAL_FILE,
    [switch]$TrustLocalSqlCertificate,
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\backup\SqlBackupSupport.ps1')
. (Join-Path $PSScriptRoot '..\operations\ScriptCompatibility.ps1')
$ApplicationFolder=Assert-BitacoraBackupLocalPath $ApplicationFolder
$EvidenceRoot=Assert-BitacoraBackupLocalPath $EvidenceRoot
$oldDeploy='C:\BitacoraEvidencias\deploy'
if ($ApplicationFolder.TrimEnd('\').Equals($oldDeploy,[StringComparison]::OrdinalIgnoreCase) -or $ApplicationFolder.StartsWith($oldDeploy+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Conservar el despliegue SQLite; usar una publicacion SQL independiente.' }
if ([uri]::CheckHostName($ServerAddress) -eq [UriHostNameType]::Unknown -or $ServerAddress.Contains(':')) { throw 'Usar nombre DNS o IPv4 del certificado, sin esquema, ruta ni puerto.' }
$dll=Join-Path $ApplicationFolder 'BitacoraEvidencias.Web.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf) -or -not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)) { throw 'Faltan binarios SQL o carpeta de evidencias existente. No se crean datos.' }
Assert-BitacoraPublishedVersion -ApplicationDll $dll -Provider SqlServer
foreach ($name in @('BITACORA_DATA_PROTECTION_KEY','BITACORA_HTTPS_CERT_PATH','BITACORA_HTTPS_CERT_PASSWORD')) {
    if (-not [Environment]::GetEnvironmentVariable($name,'Process')) { throw ('Falta '+$name+' en este proceso; no se sustituye ni genera otro secreto.') }
}
if (-not (Test-Path -LiteralPath $env:BITACORA_HTTPS_CERT_PATH -PathType Leaf)) { throw 'No existe el certificado HTTPS configurado.' }
if (Get-NetTCPConnection -LocalPort $HttpsPort -State Listen -ErrorAction SilentlyContinue) { throw 'Puerto ocupado; no se detiene la instalacion existente.' }
if ($ValidateOnly) { Write-Host 'Binarios, rutas, variables y puerto comprobados. No se conecto a SQL ni se valido el contenido del certificado. No se inicio la aplicacion.'; return }
if ($SqlCredentialFile) {
    $credential=Import-Clixml -LiteralPath $SqlCredentialFile
    if ($credential -isnot [Management.Automation.PSCredential]) { throw 'Credencial SQL no valida para esta cuenta Windows.' }
} else {
    [string]$sqlUser=Read-Host 'Usuario SQL de operacion'
    $password=Read-Host 'Contrasena SQL' -AsSecureString
    $credential=[Management.Automation.PSCredential]::new($sqlUser,$password)
}
$connection=New-BitacoraBackupConnection -DatabaseName BitacoraEvidencias -Credential $credential -TrustLocalSqlCertificate:$TrustLocalSqlCertificate
$settings=@{
    ASPNETCORE_ENVIRONMENT='Production'; ASPNETCORE_URLS="https://${ServerAddress}:$HttpsPort"
    ASPNETCORE_Kestrel__Certificates__Default__Path=$env:BITACORA_HTTPS_CERT_PATH
    ASPNETCORE_Kestrel__Certificates__Default__Password=$env:BITACORA_HTTPS_CERT_PASSWORD
    ConnectionStrings__DefaultConnection=$connection.ConnectionString; Storage__RootPath=$EvidenceRoot
    Startup__PrepareApplicationData='false'; Startup__ResetApplicationData='false'; Startup__CheckLegacySchemaOnly='false'
    Startup__StampLegacyBaselineOnly='false'; Security__ProtectExistingData='false'
}
$previous=@{}
try {
    foreach ($name in $settings.Keys) { $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process'); [Environment]::SetEnvironmentVariable($name,[string]$settings[$name],'Process') }
    Write-Host 'Arranque SQL productivo solicitado; la aplicacion exigira esquema/usuarios migrados. Ctrl+C para detener.'
    Push-Location $ApplicationFolder
    try { & dotnet $dll; if ($LASTEXITCODE -ne 0) { throw 'La aplicacion SQL termino con error; revisar su log.' } } finally { Pop-Location }
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
    $connection.Dispose()
    Remove-Variable credential,password,connection,settings -ErrorAction SilentlyContinue
}
