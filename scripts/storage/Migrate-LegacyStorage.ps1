param(
    [string]$LegacyStoragePath = "",
    [string]$TargetStorageRoot = "C:\BitacoraEvidencias\Evidencias"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '..\operations\ScriptCompatibility.ps1')
Assert-BitacoraSqliteSource

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

function Resolve-StoragePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $projectRoot $PathValue))
}

if ([string]::IsNullOrWhiteSpace($LegacyStoragePath)) {
    $LegacyStoragePath = Join-Path $projectRoot "Storage"
}

$legacyFullPath = Resolve-StoragePath -PathValue $LegacyStoragePath
$targetFullPath = Resolve-StoragePath -PathValue $TargetStorageRoot

if ($legacyFullPath.Equals($targetFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Host "La ruta legado y la ruta destino son la misma. No hay migracion pendiente."
    exit 0
}

if (-not (Test-Path $legacyFullPath)) {
    Write-Host "No se encontro almacenamiento legado en: $legacyFullPath"
    exit 0
}

$targetParent = Split-Path -Path $targetFullPath -Parent
if (-not [string]::IsNullOrWhiteSpace($targetParent)) {
    New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
}

if (-not (Test-Path $targetFullPath)) {
    Move-Item -Path $legacyFullPath -Destination $targetFullPath
    Write-Host "Migracion completada."
    Write-Host "  Origen: $legacyFullPath"
    Write-Host "  Destino: $targetFullPath"
    exit 0
}

if (-not (Get-ChildItem -Path $targetFullPath -Force | Select-Object -First 1)) {
    Remove-Item -Path $targetFullPath -Recurse -Force
    Move-Item -Path $legacyFullPath -Destination $targetFullPath
    Write-Host "Migracion completada."
    Write-Host "  Origen: $legacyFullPath"
    Write-Host "  Destino: $targetFullPath"
    exit 0
}

$movedFiles = 0
Get-ChildItem -Path $legacyFullPath -File -Recurse | ForEach-Object {
    $relativePath = [System.IO.Path]::GetRelativePath($legacyFullPath, $_.FullName)
    $destinationFile = Join-Path $targetFullPath $relativePath
    $destinationDirectory = Split-Path -Path $destinationFile -Parent

    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Move-Item -Path $_.FullName -Destination $destinationFile -Force
    $movedFiles++
}

Remove-Item -Path $legacyFullPath -Recurse -Force

Write-Host "Migracion completada."
Write-Host "  Origen: $legacyFullPath"
Write-Host "  Destino: $targetFullPath"
Write-Host "  Archivos movidos: $movedFiles"
