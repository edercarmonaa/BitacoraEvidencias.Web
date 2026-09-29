param(
    [string]$ProjectPath = ".\BitacoraEvidencias.Web.csproj",
    [string]$Configuration = "Release",
    [string]$DatabasePath,
    [string]$StorageRoot,
    [bool]$CreateBackup = $false,
    [string]$BackupRoot = "",
    [int]$BackupRetentionDays = 30
)

$ErrorActionPreference = "Stop"

$keyName = "BITACORA_DATA_PROTECTION_KEY"
$keyValue = [Environment]::GetEnvironmentVariable($keyName, "Process")

if ([string]::IsNullOrWhiteSpace($keyValue)) {
    $keyValue = [Environment]::GetEnvironmentVariable($keyName, "Machine")
}

if ([string]::IsNullOrWhiteSpace($keyValue)) {
    $keyValue = [Environment]::GetEnvironmentVariable($keyName, "User")
}

if ([string]::IsNullOrWhiteSpace($keyValue)) {
    throw "Configura la variable de entorno $keyName a nivel de sistema o usuario antes de ejecutar este script."
}

$env:BITACORA_DATA_PROTECTION_KEY = $keyValue

$projectFullPath = (Resolve-Path $ProjectPath).Path
$projectDirectory = Split-Path -Parent $projectFullPath
$appsettingsPath = Join-Path $projectDirectory "appsettings.json"

if (-not (Test-Path $appsettingsPath)) {
    throw "No se encontro appsettings.json junto al proyecto: $appsettingsPath"
}

$appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
$effectiveStorageRoot = $StorageRoot
if ([string]::IsNullOrWhiteSpace($effectiveStorageRoot)) {
    $effectiveStorageRoot = [Environment]::GetEnvironmentVariable("Storage__RootPath", "Process")
}
if ([string]::IsNullOrWhiteSpace($effectiveStorageRoot)) {
    $effectiveStorageRoot = [Environment]::GetEnvironmentVariable("Storage__RootPath", "Machine")
}
if ([string]::IsNullOrWhiteSpace($effectiveStorageRoot)) {
    $effectiveStorageRoot = [Environment]::GetEnvironmentVariable("Storage__RootPath", "User")
}
if ([string]::IsNullOrWhiteSpace($effectiveStorageRoot)) {
    $effectiveStorageRoot = $appsettings.Storage.RootPath
}

$connectionString = $null
if (-not [string]::IsNullOrWhiteSpace($DatabasePath)) {
    $effectiveDatabasePath = $DatabasePath
    if ((Test-Path $effectiveDatabasePath -PathType Container)) {
        $effectiveDatabasePath = Join-Path $effectiveDatabasePath "bitacora-evidencias.db"
    }

    $connectionString = "Data Source=$effectiveDatabasePath"
}
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Process")
}
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Machine")
}
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = [Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "User")
}
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = $appsettings.ConnectionStrings.DefaultConnection
}

$databaseSource = $connectionString -replace "^Data Source=", ""

if (-not [System.IO.Path]::IsPathRooted($effectiveStorageRoot)) {
    $effectiveStorageRoot = Join-Path $projectDirectory $effectiveStorageRoot
}

if (-not [System.IO.Path]::IsPathRooted($databaseSource)) {
    $databaseSource = Join-Path $projectDirectory $databaseSource
}

Write-Host "Proyecto: $projectFullPath"
Write-Host "Base SQLite: $databaseSource"
Write-Host "Carpeta de evidencias: $effectiveStorageRoot"
Write-Host "Respaldo previo: $CreateBackup"

if (-not (Test-Path $databaseSource)) {
    throw "No existe la base SQLite indicada: $databaseSource"
}

if (-not (Test-Path $effectiveStorageRoot)) {
    throw "No existe la carpeta de evidencias indicada: $effectiveStorageRoot"
}

$env:ConnectionStrings__DefaultConnection = "Data Source=$databaseSource"
$env:Storage__RootPath = $effectiveStorageRoot

if ($CreateBackup) {
    $backupScriptPath = Join-Path $projectDirectory "scripts\backup\Backup-Bitacora.ps1"
    if (-not (Test-Path $backupScriptPath)) {
        throw "No se encontro el script de respaldo: $backupScriptPath"
    }

    Write-Host "Ejecutando respaldo previo..."
    & $backupScriptPath -BackupRoot $BackupRoot -RetentionDays $BackupRetentionDays -ProjectRoot $projectDirectory
    Write-Host "Respaldo previo terminado."
}
else {
    Write-Host "Respaldo previo omitido por parametro. Confirma que ya existe un respaldo reciente antes de continuar."
}

Write-Host "Protegiendo datos sensibles existentes..."
dotnet run --project $ProjectPath --configuration $Configuration --no-restore -- --Security:ProtectExistingData=true

$encryptedPrefix = [System.Text.Encoding]::ASCII.GetBytes("BITACORA-ENC-V1")
$imageExtensions = @(".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff")
$files = Get-ChildItem -LiteralPath $effectiveStorageRoot -Recurse -File |
    Where-Object { $imageExtensions -contains $_.Extension.ToLowerInvariant() }

$encryptedCount = 0
$plainCount = 0
$samplePlain = @()

foreach ($file in $files) {
    $buffer = New-Object byte[] $encryptedPrefix.Length
    $read = 0
    $stream = [System.IO.File]::Open($file.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $read = $stream.Read($buffer, 0, $buffer.Length)
    }
    finally {
        $stream.Dispose()
    }

    $isEncrypted = $read -eq $encryptedPrefix.Length
    if ($isEncrypted) {
        for ($i = 0; $i -lt $encryptedPrefix.Length; $i++) {
            if ($buffer[$i] -ne $encryptedPrefix[$i]) {
                $isEncrypted = $false
                break
            }
        }
    }

    if ($isEncrypted) {
        $encryptedCount++
    }
    else {
        $plainCount++
        if ($samplePlain.Count -lt 5) {
            $samplePlain += $file.FullName
        }
    }
}

Write-Host "Proceso terminado."
Write-Host "Evidencias revisadas: $($files.Count)"
Write-Host "Evidencias cifradas: $encryptedCount"
Write-Host "Evidencias sin cifrar: $plainCount"

if ($plainCount -gt 0) {
    Write-Warning "Se encontraron evidencias sin cifrar. Primeros ejemplos:"
    $samplePlain | ForEach-Object { Write-Warning $_ }
}
