param(
    [string]$DataDbPath = "C:\BitacoraEvidencias\BitacoraEvidencias.Web\Data\bitacora-evidencias.db",
    [string]$StorageRoot = "C:\BitacoraEvidencias\Evidencias",
    [string]$EnvironmentName = "Development",
    [string]$BootstrapAdminPassword = "",
    [switch]$ResetDatabase,
    [switch]$AllowLegacySchemaWithoutMigrations
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectFile = Join-Path $projectRoot "BitacoraEvidencias.Web.csproj"

if (-not (Test-Path $projectFile)) {
    throw "No se encontro el proyecto en: $projectFile"
}

$dbFolder = Split-Path -Path $DataDbPath -Parent
if (-not [string]::IsNullOrWhiteSpace($dbFolder)) {
    New-Item -ItemType Directory -Path $dbFolder -Force | Out-Null
}

if ($ResetDatabase) {
    $sqliteArtifacts = @(
        $DataDbPath,
        "$DataDbPath-wal",
        "$DataDbPath-shm"
    )

    foreach ($artifact in $sqliteArtifacts) {
        if (Test-Path $artifact) {
            Remove-Item -Path $artifact -Force
        }
    }
}

New-Item -ItemType Directory -Path $StorageRoot -Force | Out-Null

$env:ASPNETCORE_ENVIRONMENT = $EnvironmentName
$env:ConnectionStrings__DefaultConnection = "Data Source=$DataDbPath"
$env:Storage__RootPath = $StorageRoot
$env:Startup__PrepareApplicationData = "true"
$env:Startup__ExitAfterPrepareApplicationData = "true"
$env:Startup__ResetApplicationData = if ($ResetDatabase) { "true" } else { "false" }
$env:Startup__AllowLegacySchemaWithoutMigrations = if ($AllowLegacySchemaWithoutMigrations) { "true" } else { "false" }

if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminPassword)) {
    $env:Security__BootstrapAdminPassword = $BootstrapAdminPassword
}

Write-Host "Preparando base de datos de forma explicita..."
Write-Host "  Proyecto: $projectFile"
Write-Host "  DB: $DataDbPath"
Write-Host "  Evidencias: $StorageRoot"
Write-Host "  Entorno: $EnvironmentName"
Write-Host "  AllowLegacySchemaWithoutMigrations: $AllowLegacySchemaWithoutMigrations"
Write-Host ""

dotnet run --project $projectFile --no-launch-profile
