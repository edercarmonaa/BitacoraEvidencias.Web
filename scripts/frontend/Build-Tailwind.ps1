param(
    [switch]$Watch,
    [switch]$Minify
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$tailwindExe = Join-Path $projectRoot "tools\tailwindcss-windows-x64.exe"
$configPath = Join-Path $projectRoot "tailwind.config.js"
$inputPath = Join-Path $projectRoot "wwwroot\css\app.tailwind.input.css"
$outputPath = Join-Path $projectRoot "wwwroot\css\app.tailwind.css"

if (-not (Test-Path $tailwindExe)) {
    throw "No se encontro $tailwindExe. Descargalo con scripts/frontend/Setup-FrontendAssets.ps1."
}

if (-not (Test-Path $configPath)) {
    throw "No se encontro $configPath."
}

if (-not (Test-Path $inputPath)) {
    throw "No se encontro $inputPath."
}

$args = @(
    "-c", $configPath,
    "-i", $inputPath,
    "-o", $outputPath
)

if ($Watch) {
    $args += "--watch"
}

if ($Minify) {
    $args += "--minify"
}

Write-Host "Tailwind build:"
Write-Host "  config: $configPath"
Write-Host "  input : $inputPath"
Write-Host "  output: $outputPath"
Write-Host ""

& $tailwindExe @args
