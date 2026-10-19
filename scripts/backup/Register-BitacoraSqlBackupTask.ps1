param(
    [string]$SqlCredentialFile = $env:BITACORA_BACKUP_SQL_CREDENTIAL_FILE,
    [string]$SqlBackupDirectory = $env:BITACORA_BACKUP_SQL_STAGING,
    [string]$BackupRoot = '',
    [switch]$TrustLocalSqlCertificate
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SqlBackupSupport.ps1')
$name = 'BitacoraEvidencias-SqlBackup-Diario'
if (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue) { throw 'La tarea SQL ya existe. No se sobrescribe.' }
if ([TimeZoneInfo]::Local.Id -notin @('Central Standard Time (Mexico)','America/Mexico_City')) { throw 'El horario 14:00 requiere revisar la zona horaria de Mexico del equipo antes de registrar la tarea.' }
if (-not $SqlCredentialFile -or -not (Test-Path -LiteralPath $SqlCredentialFile -PathType Leaf)) { throw 'Prepara la credencial SQL protegida con la cuenta Windows de la aplicacion.' }
if (-not $SqlBackupDirectory -or -not (Test-Path -LiteralPath $SqlBackupDirectory -PathType Container)) { throw 'Prepara la carpeta SQL de intercambio y sus permisos.' }
$SqlCredentialFile = Assert-BitacoraBackupLocalPath $SqlCredentialFile
$SqlBackupDirectory = Assert-BitacoraBackupLocalPath $SqlBackupDirectory
if (-not $BackupRoot) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT','Process') }
if (-not $BackupRoot) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT','User') }
if (-not $BackupRoot) { $BackupRoot = 'C:\BitacoraEvidencias\BackupsBitacora' }
$BackupRoot = Assert-BitacoraBackupLocalPath $BackupRoot
$runner = Join-Path $PSScriptRoot 'Invoke-BitacoraScheduledSqlBackup.ps1'
$taskArguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $runner + '" -SqlCredentialFile "' + $SqlCredentialFile + '" -SqlBackupDirectory "' + $SqlBackupDirectory + '" -BackupRoot "' + $BackupRoot + '"'
if ($TrustLocalSqlCertificate) { $taskArguments += ' -TrustLocalSqlCertificate' }
$action = New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -Argument $taskArguments
$trigger = New-ScheduledTaskTrigger -Daily -At ([datetime]::Today.AddHours(14))
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit ([timespan]::Zero)
# No catch-up outside the approved window. Activation happens at SQL cutover.
$settings.StartWhenAvailable = $false
$settings.Enabled = $false
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited
$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Respaldo SQL 0.2.0 a las 14:00; cierre ordenado, BAK/evidencias y reinicio. Activar solo tras migrar produccion a SQL.'
Register-ScheduledTask -TaskName $name -InputObject $task | Out-Null
Write-Host 'Tarea SQL creada DESHABILITADA a las 14:00 de Mexico, con la cuenta Windows actual. La tarea SQLite no se modifico. Activar solo en el cambio productivo.'
