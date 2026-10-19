param(
    [string]$SqlCredentialFile = $env:BITACORA_OPERATIONS_SQL_CREDENTIAL_FILE,
    [string]$BackupRoot = '',
    [string]$ArchiveRoot = 'C:\BitacoraEvidencias\AuditArchive',
    [string]$MonitorRoot = 'C:\BitacoraEvidencias\Monitoreo',
    [string]$EvidenceRoot = 'C:\BitacoraEvidencias\Evidencias',
    [string]$AppUrl = 'https://127.0.0.1:5067/Login',
    [switch]$TrustLocalSqlCertificate
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SqlOperationsSupport.ps1')
if ([TimeZoneInfo]::Local.Id -notin @('Central Standard Time (Mexico)','America/Mexico_City')) { throw 'Revisar zona horaria de Mexico antes de registrar tareas.' }
if (-not $SqlCredentialFile -or -not (Test-Path -LiteralPath $SqlCredentialFile -PathType Leaf)) { throw 'Preparar primero la credencial SQL protegida con la cuenta Windows de las tareas.' }
$credentialProbe = Import-Clixml -LiteralPath $SqlCredentialFile
if ($credentialProbe -isnot [Management.Automation.PSCredential]) { throw 'Credencial SQL no valida para esta cuenta.' }
Remove-Variable credentialProbe
foreach ($path in @($SqlCredentialFile,$ArchiveRoot,$MonitorRoot,$EvidenceRoot)) { [void](Assert-BitacoraBackupLocalPath $path) }
$uri = $null
if (-not [uri]::TryCreate($AppUrl,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -notin @('https','http') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $AppUrl.Contains('"')) { throw 'URL de disponibilidad no valida.' }
$definitions = @(
    @{ Name = 'BitacoraEvidencias-SqlMonitor-Diario'; Time = [datetime]::Today.AddHours(9).AddMinutes(15); Script = '..\monitor\Monitor-Bitacora.ps1'; Extra = '-MonitorRoot "' + $MonitorRoot + '" -EvidenceRoot "' + $EvidenceRoot + '" -AppUrl "' + $AppUrl + '"' },
    @{ Name = 'BitacoraEvidencias-SqlAuditArchive-Diario'; Time = [datetime]::Today.AddHours(9).AddMinutes(20); Script = '..\audit\Archive-AuditLog.ps1'; Extra = '-Mode Archive -RetentionDays 30 -ArchiveRoot "' + $ArchiveRoot + '"' }
)
foreach ($definition in $definitions) { if (Get-ScheduledTask -TaskName $definition.Name -ErrorAction SilentlyContinue) { throw 'Ya existe una tarea SQL. No se sobrescribe ninguna tarea.' } }
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
foreach ($definition in $definitions) {
    $runner = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $definition.Script))
    $arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $runner + '" -SqlCredentialFile "' + $SqlCredentialFile + '" -NonInteractive ' + $definition.Extra
    if ($definition.Name -like '*SqlMonitor*' -and $BackupRoot) { [void](Assert-BitacoraBackupLocalPath $BackupRoot); $arguments += ' -BackupRoot "' + $BackupRoot + '"' }
    if ($TrustLocalSqlCertificate) { $arguments += ' -TrustLocalSqlCertificate' }
    $action = New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -Argument $arguments
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit ([timespan]::Zero)
    $settings.Enabled = $false; $settings.StartWhenAvailable = $false
    $trigger = New-ScheduledTaskTrigger -Daily -At $definition.Time
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'SQL Server 0.2.0: preparada, activar solo al migrar produccion. Conserva tareas SQLite.'
    Register-ScheduledTask -TaskName $definition.Name -InputObject $task | Out-Null
    Write-Host ($definition.Name + ': creada DESHABILITADA a las ' + $definition.Time.ToString('HH:mm') + ' de Mexico.')
}
