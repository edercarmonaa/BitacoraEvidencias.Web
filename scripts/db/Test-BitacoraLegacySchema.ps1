param(
    [string]$DataDbPath = "C:\BitacoraEvidencias\Data\bitacora-evidencias.db",
    [string]$EnvironmentName = "Production"
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

$env:ASPNETCORE_ENVIRONMENT = $EnvironmentName
$env:ConnectionStrings__DefaultConnection = "Data Source=$DataDbPath"
$env:Startup__CheckLegacySchemaOnly = "true"
$env:Startup__PrepareApplicationData = "false"
$env:Startup__ExitAfterPrepareApplicationData = "false"
$env:Startup__ResetApplicationData = "false"

Write-Host "Verificando si la base usa historial de migraciones de EF Core..."
Write-Host "  Proyecto: $projectFile"
Write-Host "  DB: $DataDbPath"
Write-Host "  Entorno: $EnvironmentName"
Write-Host ""

dotnet run --project $projectFile --no-launch-profile
