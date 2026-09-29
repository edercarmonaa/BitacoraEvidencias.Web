param(
    [Parameter(Mandatory = $true)]
    [string]$ServerIp,

    [int]$HttpsPort = 5067,
    [string]$EnvironmentName = "Production",
    [string]$DataDbPath = "C:\BitacoraEvidencias\Data\bitacora-evidencias.db",
    [string]$StorageRoot = "C:\BitacoraEvidencias\Evidencias"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-PfxCredential {
    param(
        [string]$Path,
        [string]$Password
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        [string]::IsNullOrWhiteSpace($Password) -or
        -not (Test-Path $Path)) {
        return $false
    }

    try {
        $certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($Path, $Password)
        $certificate.Dispose()
        return $true
    }
    catch {
        return $false
    }
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectFile = Join-Path $projectRoot "BitacoraEvidencias.Web.csproj"

if (-not (Test-Path $projectFile)) {
    throw "No se encontro el proyecto en: $projectFile"
}

$legacyStoragePath = Join-Path $projectRoot "Storage"

if (-not (Test-Path $DataDbPath)) {
    throw "No se encontro la base de datos en: $DataDbPath. Ejecuta .\scripts\db\Initialize-BitacoraDatabase.ps1 para prepararla de forma explicita."
}

if (-not (Test-Path $StorageRoot)) {
    throw "No se encontro la carpeta de evidencias en: $StorageRoot"
}

if ((Test-Path $legacyStoragePath) -and
    -not ([System.IO.Path]::GetFullPath($legacyStoragePath)).Equals([System.IO.Path]::GetFullPath($StorageRoot), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Se detecto almacenamiento legado en: $legacyStoragePath. Ejecuta .\scripts\storage\Migrate-LegacyStorage.ps1 antes de iniciar la aplicacion."
}

$processCertPath = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PATH", "Process")
$processCertPassword = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PASSWORD", "Process")
$userCertPath = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PATH", "User")
$userCertPassword = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PASSWORD", "User")

$certPath = $null
$certPassword = $null
$certSource = $null

if (Test-PfxCredential -Path $processCertPath -Password $processCertPassword) {
    $certPath = $processCertPath
    $certPassword = $processCertPassword
    $certSource = "Process"
}
elseif (Test-PfxCredential -Path $userCertPath -Password $userCertPassword) {
    $certPath = $userCertPath
    $certPassword = $userCertPassword
    $certSource = "User"
}
elseif ([string]::IsNullOrWhiteSpace($processCertPath) -and [string]::IsNullOrWhiteSpace($userCertPath)) {
    throw "BITACORA_HTTPS_CERT_PATH no esta definido ni en Process ni en User. Ejecuta primero New-LanCertificate.ps1."
}
else {
    throw "No se pudo abrir el certificado configurado. Revisa BITACORA_HTTPS_CERT_PATH y BITACORA_HTTPS_CERT_PASSWORD en Process/User o regenera el certificado con New-LanCertificate.ps1."
}

$env:ASPNETCORE_URLS = "https://$ServerIp`:$HttpsPort"
$env:ASPNETCORE_Kestrel__Certificates__Default__Path = $certPath
$env:ASPNETCORE_Kestrel__Certificates__Default__Password = $certPassword
$env:ASPNETCORE_ENVIRONMENT = $EnvironmentName
$env:ConnectionStrings__DefaultConnection = "Data Source=$DataDbPath"
$env:Storage__RootPath = $StorageRoot

Write-Host "Servidor HTTPS estricto (sin HTTP)"
Write-Host "  URL: https://$ServerIp`:$HttpsPort"
Write-Host "  Entorno ASPNETCORE_ENVIRONMENT: $EnvironmentName"
Write-Host "  DB: $DataDbPath"
Write-Host "  Evidencias: $StorageRoot"
Write-Host "  Certificado: $certPath"
Write-Host "  Origen certificado: $certSource"
Write-Host ""

dotnet run --project $projectFile --no-launch-profile
