param([Parameter(Mandatory = $true)][string]$Directory, [string]$TaskAccount = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SqlBackupSupport.ps1')
$target = Assert-BitacoraBackupLocalPath $Directory
if ($target.TrimEnd('\') -eq [IO.Path]::GetPathRoot($target).TrimEnd('\')) { throw 'No se cambian permisos de una raiz de disco.' }
if (Test-Path -LiteralPath $target) {
    if (@(Get-ChildItem -LiteralPath $target -Force).Count -gt 0) { throw 'Usa una carpeta dedicada vacia para preparar los permisos.' }
    if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se permiten enlaces o junctions.' }
} else { New-Item -ItemType Directory -Path $target | Out-Null }
$current = [Security.Principal.WindowsIdentity]::GetCurrent().User
$taskSid = if ($TaskAccount) { [Security.Principal.NTAccount]::new($TaskAccount).Translate([Security.Principal.SecurityIdentifier]) } else { $current }
$sqlSid = [Security.Principal.NTAccount]::new('NT SERVICE\MSSQL$Auditoria').Translate([Security.Principal.SecurityIdentifier])
$acl = Get-Acl -LiteralPath $target
$acl.SetAccessRuleProtection($true,$false)
$inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
$propagation = [Security.AccessControl.PropagationFlags]::None
$allow = [Security.AccessControl.AccessControlType]::Allow
foreach ($sid in @($current,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'),[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,$inherit,$propagation,$allow))
}
foreach ($sid in @($taskSid,$sqlSid)) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::Modify,$inherit,$propagation,$allow))
}
Set-Acl -LiteralPath $target -AclObject $acl
Write-Host "Carpeta SQL preparada: $target. Permisos limitados al servicio SQL, cuenta de tarea, creador, SYSTEM y administradores."
