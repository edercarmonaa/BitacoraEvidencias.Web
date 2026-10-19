Set-StrictMode -Version Latest

function Assert-BitacoraSqliteSource {
    param([string]$ProjectFile = (Join-Path $PSScriptRoot '..\..\BitacoraEvidencias.Web.csproj'))
    $project = [xml][IO.File]::ReadAllText([IO.Path]::GetFullPath($ProjectFile))
    $version = [string](@($project.Project.PropertyGroup | Where-Object { $_.PSObject.Properties.Name -contains 'Version' })[0].Version)
    if ($version -notmatch '^0\.1\.') {
        throw 'Procedimiento exclusivo SQLite 0.1.0: detenido antes de modificar archivos o entorno. Usa un worktree separado de v0.1.0; para SQL consulta docs/INDEX.md.'
    }
}

function Assert-BitacoraPublishedVersion {
    param([string]$ApplicationDll,[ValidateSet('Sqlite','SqlServer')][string]$Provider)
    $version = [Reflection.AssemblyName]::GetAssemblyName($ApplicationDll).Version
    if ($Provider -eq 'Sqlite') {
        # The original installation predates the 0.1.0 version property and has
        # AssemblyVersion 1.0.0.0. Check its actual provider dependencies too.
        $legacyVersion = ($version.Major -eq 0 -and $version.Minor -eq 1) -or $version.ToString() -eq '1.0.0.0'
        $depsPath=[IO.Path]::ChangeExtension($ApplicationDll,'deps.json')
        if (-not $legacyVersion -or -not (Test-Path -LiteralPath $depsPath -PathType Leaf)) { throw 'Binarios SQLite no reconocidos; no se inicia.' }
        $deps=[IO.File]::ReadAllText($depsPath) | ConvertFrom-Json
        $names=@($deps.libraries.PSObject.Properties.Name)
        if (@($names | Where-Object { $_ -like 'Microsoft.EntityFrameworkCore.Sqlite/*' }).Count -eq 0 -or @($names | Where-Object { $_ -like 'Microsoft.EntityFrameworkCore.SqlServer/*' }).Count -gt 0) { throw 'El proveedor del binario no corresponde al lanzador SQLite.' }
        return
    }
    $minor = if ($Provider -eq 'Sqlite') { 1 } else { 2 }
    if ($version.Major -ne 0 -or $version.Minor -ne $minor) { throw 'Los binarios no corresponden al proveedor/version del lanzador. No se inicia la aplicacion.' }
}
