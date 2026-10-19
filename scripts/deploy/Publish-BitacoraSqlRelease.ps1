param([string]$ReleaseRoot = 'C:\BitacoraEvidencias\deploy-sql\releases',[switch]$NoRestore)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '..\backup\SqlBackupSupport.ps1')
. (Join-Path $PSScriptRoot '..\operations\ScriptCompatibility.ps1')
$ReleaseRoot=Assert-BitacoraBackupLocalPath $ReleaseRoot
$oldDeploy=[IO.Path]::GetFullPath('C:\BitacoraEvidencias\deploy')
if ($ReleaseRoot.TrimEnd('\').Equals($oldDeploy,[StringComparison]::OrdinalIgnoreCase) -or $ReleaseRoot.StartsWith($oldDeploy+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'No se publica SQL en el despliegue SQLite anterior.' }
foreach ($data in @('Data','Evidencias','AuditArchive','BackupsBitacora','Secrets','certs')) {
    $protected=Join-Path 'C:\BitacoraEvidencias' $data
    if ($ReleaseRoot.TrimEnd('\').Equals($protected,[StringComparison]::OrdinalIgnoreCase) -or $ReleaseRoot.StartsWith($protected+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'La publicacion no puede escribirse dentro de datos o secretos.' }
}
$target=Assert-BitacoraBackupChild (Join-Path $ReleaseRoot ('0.2.0_'+(Get-Date -Format yyyyMMdd_HHmmss)+'_'+[guid]::NewGuid().ToString('N'))) $ReleaseRoot
if (Test-Path -LiteralPath $target) { throw 'El release ya existe; no se sobrescribe.' }
$project=Join-Path $PSScriptRoot '..\..\BitacoraEvidencias.Web.csproj'
$publishArguments=@('publish',$project,'-c','Release','-o',$target,'--no-self-contained')
if ($NoRestore) { $publishArguments+='--no-restore' }
& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) { throw 'Fallo la publicacion SQL; carpeta conservada para revision. No se activa ni inicia.' }
Assert-BitacoraPublishedVersion -ApplicationDll (Join-Path $target 'BitacoraEvidencias.Web.dll') -Provider SqlServer
[ordered]@{ provider='SqlServer'; version='0.2.0'; createdAtUtc=[datetime]::UtcNow.ToString('o'); activated=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $target 'sql-release.json') -Encoding UTF8
Write-Host ('Release SQL preparado: '+$target+'. No se cambiaron binarios SQLite, base, tareas ni lanzador activo.')
