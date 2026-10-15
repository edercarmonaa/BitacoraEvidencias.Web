param(
    [Parameter(Mandatory = $true)][string]$BackupZip,
    [string]$TrialRoot = 'C:\BitacoraEvidencias_Pruebas'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$destination = [IO.Path]::GetFullPath((Join-Path $TrialRoot 'Input'))
$productionRoot = [IO.Path]::GetFullPath('C:\BitacoraEvidencias')
if ($destination.Equals($productionRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $destination.StartsWith($productionRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'El destino no puede estar dentro de produccion.'
}
if (Test-Path -LiteralPath $destination) { throw 'Input ya existe. Usa una carpeta nueva; no se sobrescriben copias.' }
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $BackupZip).Path)
try {
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        $relative = $entry.FullName.Replace('/', '\')
        $target = [IO.Path]::GetFullPath((Join-Path $destination $relative))
        if (-not $target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::IsPathRooted($relative) -or -not $names.Add($target)) { throw 'Archivo ZIP con rutas no validas o duplicadas.' }
    }
    New-Item -ItemType Directory -Path $destination | Out-Null
    $files = 0
    foreach ($entry in $archive.Entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $destination $entry.FullName.Replace('/', '\')))
        if ([string]::IsNullOrEmpty($entry.Name)) {
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            continue
        }
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        $original = $entry.Open()
        $copy = [IO.File]::OpenRead($target)
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            $expected = [Convert]::ToBase64String($sha.ComputeHash($original))
            $actual = [Convert]::ToBase64String($sha.ComputeHash($copy))
            if ($actual -ne $expected) { throw 'La copia no coincide con el respaldo.' }
        } finally { $sha.Dispose(); $original.Dispose(); $copy.Dispose() }
        $files++
    }
    if (-not (Test-Path -LiteralPath (Join-Path $destination 'Data\bitacora-evidencias.db')) -or
        -not (Test-Path -LiteralPath (Join-Path $destination 'Evidencias'))) { throw 'El respaldo no contiene la estructura esperada.' }
    Write-Host "Respaldo extraido y comparado por SHA256: $files archivos. Destino: $destination"
} finally { $archive.Dispose() }
