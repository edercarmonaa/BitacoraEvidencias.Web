param(
    [Parameter(Mandatory = $true)]
    [string]$ServerIp,

    [int]$HttpsPort = 5067,
    [string]$EnvironmentName = "Production",
    [string]$DeployRoot = "C:\BitacoraEvidencias\deploy",
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

$currentDir = Join-Path $DeployRoot "current"
$appDll = Join-Path $currentDir "BitacoraEvidencias.Web.dll"

if (-not (Test-Path $appDll)) {
    throw "No se encontro app publicada en: $appDll. Ejecuta primero Publish-Bitacora.ps1."
}

. (Join-Path $PSScriptRoot '..\operations\ScriptCompatibility.ps1')
Assert-BitacoraPublishedVersion -ApplicationDll $appDll -Provider Sqlite

if (-not (Test-Path $DataDbPath)) {
    throw "No se encontro la base de datos en: $DataDbPath. Ejecuta .\scripts\db\Initialize-BitacoraDatabase.ps1 para prepararla de forma explicita."
}

if (-not (Test-Path $StorageRoot)) {
    throw "No se encontro la carpeta de evidencias en: $StorageRoot"
}

$processCertPath = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PATH", "Process")
$processCertPassword = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PASSWORD", "Process")
$userCertPath = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PATH", "User")
$userCertPassword = [Environment]::GetEnvironmentVariable("BITACORA_HTTPS_CERT_PASSWORD", "User")
$processDataProtectionKey = [Environment]::GetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "Process")
$machineDataProtectionKey = [Environment]::GetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "Machine")
$userDataProtectionKey = [Environment]::GetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "User")

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
    throw "BITACORA_HTTPS_CERT_PATH no esta definido ni en Process ni en User."
}
else {
    throw "No se pudo abrir el certificado configurado. Revisa BITACORA_HTTPS_CERT_PATH y BITACORA_HTTPS_CERT_PASSWORD en Process/User o regenera el certificado con New-LanCertificate.ps1."
}

$dataProtectionKey = $null
$dataProtectionKeySource = $null
if (-not [string]::IsNullOrWhiteSpace($processDataProtectionKey)) {
    $dataProtectionKey = $processDataProtectionKey
    $dataProtectionKeySource = "Process"
}
elseif (-not [string]::IsNullOrWhiteSpace($machineDataProtectionKey)) {
    $dataProtectionKey = $machineDataProtectionKey
    $dataProtectionKeySource = "Machine"
}
elseif (-not [string]::IsNullOrWhiteSpace($userDataProtectionKey)) {
    $dataProtectionKey = $userDataProtectionKey
    $dataProtectionKeySource = "User"
}
else {
    throw "BITACORA_DATA_PROTECTION_KEY no esta definido en Process, Machine ni User. La app no podria descifrar datos sensibles."
}

$env:ASPNETCORE_URLS = "https://$ServerIp`:$HttpsPort"
$env:ASPNETCORE_Kestrel__Certificates__Default__Path = $certPath
$env:ASPNETCORE_Kestrel__Certificates__Default__Password = $certPassword
$env:ASPNETCORE_ENVIRONMENT = $EnvironmentName
$env:ConnectionStrings__DefaultConnection = "Data Source=$DataDbPath"
$env:Storage__RootPath = $StorageRoot
$env:BITACORA_DATA_PROTECTION_KEY = $dataProtectionKey

Write-Host "Servidor publicado HTTPS estricto (sin HTTP)"
Write-Host "  URL: https://$ServerIp`:$HttpsPort"
Write-Host "  Entorno: $EnvironmentName"
Write-Host "  Deploy: $currentDir"
Write-Host "  DB: $DataDbPath"
Write-Host "  Evidencias: $StorageRoot"
Write-Host "  Certificado: $certPath"
Write-Host "  Origen certificado: $certSource"
Write-Host "  Llave de cifrado: configurada ($dataProtectionKeySource)"
Write-Host ""

Push-Location $currentDir
try {
    dotnet $appDll
}
finally {
    Pop-Location
}
