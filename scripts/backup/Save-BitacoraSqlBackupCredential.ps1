param([Parameter(Mandatory = $true)][string]$CredentialPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SqlBackupSupport.ps1')
$target = Assert-BitacoraBackupLocalPath $CredentialPath
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
if ($target.StartsWith($repo + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Guarda la credencial fuera del repositorio.' }
if (Test-Path -LiteralPath $target) { throw 'El archivo ya existe; no se sobrescribe.' }
[string]$sqlUser = Read-Host 'Usuario SQL para respaldos'
if ([string]::IsNullOrWhiteSpace($sqlUser)) { throw 'El usuario SQL es obligatorio.' }
$sqlPassword = Read-Host 'Contrasena SQL' -AsSecureString
try {
    $credential = [Management.Automation.PSCredential]::new($sqlUser,$sqlPassword)
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    # Windows DPAPI: usable only on this machine, by this Windows identity.
    $credential | Export-Clixml -LiteralPath $target -NoClobber
    Write-Host 'Credencial SQL guardada con DPAPI de Windows. La tarea debe ejecutarse con esta misma cuenta de Windows en este equipo.'
} finally { Remove-Variable credential,sqlPassword -ErrorAction SilentlyContinue }
