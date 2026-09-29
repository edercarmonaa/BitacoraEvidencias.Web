param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$toolsDir = Join-Path $projectRoot "tools"
$libDir = Join-Path $projectRoot "wwwroot\lib\htmx"
$tailwindExe = Join-Path $toolsDir "tailwindcss-windows-x64.exe"
$htmxJs = Join-Path $libDir "htmx.min.js"

New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
New-Item -ItemType Directory -Path $libDir -Force | Out-Null

if ($Force -or -not (Test-Path $htmxJs)) {
    Write-Host "Descargando HTMX..."
    Invoke-WebRequest -Uri "https://unpkg.com/htmx.org@1.9.12/dist/htmx.min.js" -OutFile $htmxJs
}
else {
    Write-Host "HTMX ya existe: $htmxJs"
}

if ($Force -or -not (Test-Path $tailwindExe)) {
    Write-Host "Descargando Tailwind standalone..."
    Invoke-WebRequest -Uri "https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-windows-x64.exe" -OutFile $tailwindExe
}
else {
    Write-Host "Tailwind ya existe: $tailwindExe"
}

Write-Host ""
Write-Host "Assets frontend listos."
Write-Host "Siguiente paso:"
Write-Host "  .\\scripts\\frontend\\Build-Tailwind.ps1 -Minify"
