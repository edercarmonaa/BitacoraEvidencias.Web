param(
    [string]$DeployRoot = "C:\BitacoraEvidencias\deploy",
    [string]$Configuration = "Release",
    [int]$KeepReleases = 5,
    [string]$CreateBackup = "true",
    [string]$BackupRoot = "",
    [int]$BackupRetentionDays = 30,
    [switch]$SkipBackup,
    [switch]$NoBackup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-ToBooleanParameter {
    param(
        [string]$Value,
        [string]$Name
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $true
    }

    $normalized = $Value.Trim().ToLowerInvariant()
    switch ($normalized) {
        { $_ -in @("true", "`$true", "1", "yes", "y", "si", "s") } { return $true }
        { $_ -in @("false", "`$false", "0", "no", "n") } { return $false }
        default {
            throw "Valor invalido para -${Name}: '$Value'. Usa true/false, `$true/`$false, 1/0, si/no."
        }
    }
}

$createBackupRaw = if ($PSBoundParameters.ContainsKey("CreateBackup")) { [string]$PSBoundParameters["CreateBackup"] } else { $CreateBackup }
$shouldCreateBackup = Convert-ToBooleanParameter -Value $createBackupRaw -Name "CreateBackup"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectFile = Join-Path $projectRoot "BitacoraEvidencias.Web.csproj"
$backupScript = Join-Path $projectRoot "scripts\backup\Backup-BitacoraSqlite.ps1"
$localDotnet = Join-Path $projectRoot ".dotnet\dotnet.exe"
$dotnetExe = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }

$env:DOTNET_CLI_HOME = Join-Path $projectRoot ".dotnet-home"
$env:NUGET_PACKAGES = Join-Path $projectRoot ".nuget\packages"
$env:APPDATA = Join-Path $projectRoot ".appdata"
$env:LOCALAPPDATA = Join-Path $projectRoot ".localappdata"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if (-not (Test-Path $projectFile)) {
    throw "No se encontro el proyecto en: $projectFile"
}

$releaseStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$releaseDir = Join-Path $DeployRoot ("releases\" + $releaseStamp)
$currentDir = Join-Path $DeployRoot "current"
$logsDir = Join-Path $DeployRoot "logs"

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
New-Item -ItemType Directory -Path $currentDir -Force | Out-Null
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

if ($SkipBackup -or $NoBackup) {
    $shouldCreateBackup = $false
}

Write-Host "Parametro CreateBackup recibido: $createBackupRaw"
Write-Host "Respaldo previo: $shouldCreateBackup"

if ($shouldCreateBackup) {
    if (-not (Test-Path $backupScript)) {
        throw "No se encontro script de backup: $backupScript"
    }

    Write-Host "Ejecutando respaldo previo..."
    & $backupScript -BackupRoot $BackupRoot -RetentionDays $BackupRetentionDays -ProjectRoot $projectRoot
}
else {
    Write-Host "Respaldo previo omitido por parametro."
}

Write-Host "Publicando release en: $releaseDir"
Write-Host "Usando dotnet: $dotnetExe"
& $dotnetExe publish $projectFile -c $Configuration -o $releaseDir --no-self-contained
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo con codigo de salida $LASTEXITCODE. No se actualizo la carpeta current."
}

Write-Host "Actualizando carpeta current..."
robocopy $releaseDir $currentDir /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
$robocopyExitCode = $LASTEXITCODE
if ($robocopyExitCode -gt 7) {
    throw "robocopy fallo con codigo de salida $robocopyExitCode al actualizar current."
}

$releaseInfo = [ordered]@{
    releaseStamp = $releaseStamp
    publishedAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    configuration = $Configuration
    projectFile = $projectFile
    releaseDir = $releaseDir
    currentDir = $currentDir
    machineName = $env:COMPUTERNAME
    userName = "$env:USERDOMAIN\$env:USERNAME"
}

$releaseInfoPath = Join-Path $DeployRoot "release-info.json"
$releaseInfo | ConvertTo-Json -Depth 4 | Set-Content -Path $releaseInfoPath -Encoding UTF8

if ($KeepReleases -gt 0) {
    $releasesRoot = Join-Path $DeployRoot "releases"
    $oldReleases = Get-ChildItem -Path $releasesRoot -Directory |
        Sort-Object Name -Descending |
        Select-Object -Skip $KeepReleases

    foreach ($old in $oldReleases) {
        Write-Host "Eliminando release antiguo: $($old.FullName)"
        Remove-Item -Path $old.FullName -Recurse -Force
    }
}

Write-Host ""
Write-Host "Deploy publish completado."
Write-Host "Release actual: $releaseStamp"
Write-Host "Current: $currentDir"
Write-Host "Inicio manual sugerido:"
Write-Host ".\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>"
Write-Host "Diagnostico tecnico:"
Write-Host ".\scripts\deploy\Start-BitacoraPublished.ps1 -ServerIp <SERVER_IP>"

