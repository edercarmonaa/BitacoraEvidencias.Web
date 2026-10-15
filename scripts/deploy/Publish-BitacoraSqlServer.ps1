param(
    [string]$TrialRoot = 'C:\BitacoraEvidencias_Pruebas',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]*$')][string]$AppFolder = 'App'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$target = [IO.Path]::GetFullPath((Join-Path $TrialRoot $AppFolder))
$production = [IO.Path]::GetFullPath('C:\BitacoraEvidencias')
if ($target.Equals($production,[StringComparison]::OrdinalIgnoreCase) -or $target.StartsWith($production + '\',[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Este publicador no sustituye la instalacion productiva.'
}
if (Test-Path -LiteralPath $target) { throw 'App ya existe. Publica en otra carpeta para conservar los binarios del ensayo anterior.' }
& dotnet publish (Join-Path $root 'BitacoraEvidencias.Web.csproj') -c Release -o $target --no-self-contained --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Fallo la publicacion. No iniciar el ensayo.' }
Write-Host "SQL Server 0.2.0 publicado para ensayo en $target. No se inicio ningun proceso."
