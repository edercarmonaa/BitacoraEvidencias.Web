param(
    [string]$DataDbPath = "C:\BitacoraEvidencias\Data\bitacora-evidencias.db",
    [string]$EnvironmentName = "Production",
    [string]$BackupRoot = "",
    [switch]$SkipBackup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectFile = Join-Path $projectRoot "BitacoraEvidencias.Web.csproj"

if (-not (Test-Path $projectFile)) {
    throw "No se encontro el proyecto en: $projectFile"
}

if (-not (Test-Path $DataDbPath)) {
    throw "No se encontro la base SQLite en: $DataDbPath"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dbFolder = Split-Path -Path $DataDbPath -Parent

if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path $dbFolder "backups"
}

$backupFolder = Join-Path $BackupRoot "legacy-baseline-$timestamp"

if (-not $SkipBackup) {
    New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null

    $artifacts = @(
        $DataDbPath,
        "$DataDbPath-wal",
        "$DataDbPath-shm"
    )

    foreach ($artifact in $artifacts) {
        if (Test-Path $artifact) {
            Copy-Item -Path $artifact -Destination (Join-Path $backupFolder (Split-Path $artifact -Leaf)) -Force
        }
    }
}

$env:ASPNETCORE_ENVIRONMENT = $EnvironmentName
$env:ConnectionStrings__DefaultConnection = "Data Source=$DataDbPath"
$env:Startup__StampLegacyBaselineOnly = "true"
$env:Startup__PrepareApplicationData = "false"
$env:Startup__ExitAfterPrepareApplicationData = "false"
$env:Startup__ResetApplicationData = "false"
$env:Startup__AllowLegacySchemaWithoutMigrations = "false"

Write-Host "Registrando baseline de migraciones EF Core sobre base legacy..."
Write-Host "  Proyecto: $projectFile"
Write-Host "  DB: $DataDbPath"
Write-Host "  Entorno: $EnvironmentName"
if (-not $SkipBackup) {
    Write-Host "  Backup: $backupFolder"
} else {
    Write-Host "  Backup: omitido por -SkipBackup"
}
Write-Host ""

dotnet run --project $projectFile --no-launch-profile

if ($LASTEXITCODE -ne 0) {
    throw "El stamp del baseline no termino correctamente."
}

Write-Host ""
Write-Host "Siguiente paso recomendado:"
Write-Host "  powershell -ExecutionPolicy Bypass -File .\scripts\db\Initialize-BitacoraDatabase.ps1 -DataDbPath `"$DataDbPath`" -EnvironmentName `"$EnvironmentName`""

if (-not $SkipBackup) {
    Write-Host ""
    Write-Host "Rollback rapido:"
    Write-Host "  1. Deten la aplicacion."
    Write-Host "  2. Restaura $backupFolder\\$(Split-Path $DataDbPath -Leaf) sobre $DataDbPath"
    Write-Host "  3. Restaura tambien los archivos -wal y -shm si existen en el backup."
}
