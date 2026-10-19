param(
    [ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName = 'BitacoraEvidencias',
    [string]$BackupRoot = '',
    [string]$EvidenceRoot = '',
    [string]$SqlBackupDirectory = $env:BITACORA_BACKUP_SQL_STAGING,
    [string]$SqlCredentialFile = $env:BITACORA_BACKUP_SQL_CREDENTIAL_FILE,
    [ValidateRange(1,36500)][int]$RetentionDays = 30,
    [ValidateRange(0,65535)][int]$ApplicationPort = 0,
    [string]$ApplicationLogRoot = '',
    [switch]$UseWindowsAuthentication,
    [switch]$TrustLocalSqlCertificate,
    [switch]$SkipRetention
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SqlBackupSupport.ps1')
if ($ApplicationPort -eq 0) { $ApplicationPort = if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas') { 5068 } else { 5067 } }
if (-not $BackupRoot) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT','Process') }
if (-not $BackupRoot) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT','User') }
if (-not $BackupRoot) { $BackupRoot = 'C:\BitacoraEvidencias\BackupsBitacora' }
$BackupRoot = Assert-BitacoraBackupLocalPath $BackupRoot
if (-not $ApplicationLogRoot) { $ApplicationLogRoot = Join-Path $BackupRoot ('SqlServer\' + $DatabaseName + '\ApplicationLogs') }
$ApplicationLogRoot = Assert-BitacoraBackupLocalPath $ApplicationLogRoot
# Reject missing credentials before stopping a live application.
if (-not $UseWindowsAuthentication) {
    if (-not $SqlCredentialFile) { throw 'Prepara la credencial SQL protegida antes de programar el respaldo.' }
    $credentialProbe = Import-Clixml -LiteralPath $SqlCredentialFile
    if ($credentialProbe -isnot [Management.Automation.PSCredential]) { throw 'La credencial SQL de respaldo no es valida.' }
    Remove-Variable credentialProbe
}
$snapshot = $null
$restartRequired = $false
$backupFailed = $false
try {
    $listeners = @(Get-NetTCPConnection -LocalPort $ApplicationPort -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        $owners = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique)
        if ($owners.Count -ne 1) { throw 'El puerto no pertenece a una sola instancia.' }
        $appProcessId = [int]$owners[0]
        $pipe = [IO.Pipes.NamedPipeClientStream]::new('.',('BitacoraBackup_' + $appProcessId),[IO.Pipes.PipeDirection]::InOut,[IO.Pipes.PipeOptions]::Asynchronous)
        try {
            $pipe.Connect(10000)
            $reader = [IO.StreamReader]::new($pipe,[Text.Encoding]::UTF8,$false,1024,$true)
            $writer = [IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true)
            try {
                $writer.AutoFlush = $true
                $writer.WriteLine('PREPARE_BACKUP_V1')
                $response = $reader.ReadLineAsync()
                if (-not $response.Wait(10000)) { throw 'No se recibio la configuracion de reinicio; la aplicacion no se detiene.' }
                $snapshot = $response.Result | ConvertFrom-Json
                if ($snapshot.Version -ne 1 -or $snapshot.ProcessId -ne $appProcessId -or $snapshot.DatabaseName -cne $DatabaseName) { throw 'La instancia activa no corresponde al respaldo solicitado.' }
                $activeEvidence = Assert-BitacoraBackupLocalPath $snapshot.EvidenceRoot
                if ($EvidenceRoot -and -not (Assert-BitacoraBackupLocalPath $EvidenceRoot).TrimEnd('\').Equals($activeEvidence.TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'La carpeta de evidencias solicitada no coincide con la instancia activa.' }
                $EvidenceRoot = $activeEvidence
                $executableName = [IO.Path]::GetFileName($snapshot.Executable)
                if ($executableName -notin @('dotnet.exe','BitacoraEvidencias.Web.exe') -or
                    -not (Test-Path -LiteralPath $snapshot.Executable -PathType Leaf) -or
                    -not (Test-Path -LiteralPath $snapshot.ContentRoot -PathType Container)) { throw 'La configuracion de reinicio no es valida.' }
                if ($executableName -eq 'dotnet.exe' -and ($snapshot.Arguments.Count -ne 1 -or [IO.Path]::GetFileName($snapshot.Arguments[0]) -cne 'BitacoraEvidencias.Web.dll' -or
                    -not (Test-Path -LiteralPath $snapshot.Arguments[0] -PathType Leaf))) { throw 'El ensamblado de reinicio no corresponde a la aplicacion.' }
                if ($executableName -ne 'dotnet.exe' -and $snapshot.Arguments.Count -ne 0) { throw 'Argumentos de reinicio no admitidos.' }
                New-Item -ItemType Directory -Path $ApplicationLogRoot -Force | Out-Null
                $restartRequired = $true
                $writer.WriteLine('STOP_FOR_BACKUP_V1')
            } finally { $writer.Dispose(); $reader.Dispose() }
        } finally { $pipe.Dispose() }
        $deadline = (Get-Date).AddSeconds(120)
        while ((Get-Process -Id $appProcessId -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
        if (Get-Process -Id $appProcessId -ErrorAction SilentlyContinue) { throw 'El cierre ordenado no termino; se cancela el respaldo y no se fuerza el proceso.' }
        Assert-BitacoraBackupStopped -Port $ApplicationPort
        Write-Host 'Aplicacion detenida de forma ordenada. Configuracion de reinicio conservada solo en memoria.'
    }
    $arguments = @{
        DatabaseName = $DatabaseName; BackupRoot = $BackupRoot; SqlBackupDirectory = $SqlBackupDirectory
        SqlCredentialFile = $SqlCredentialFile; RetentionDays = $RetentionDays; ApplicationPort = $ApplicationPort
        ApplicationStopped = $true; NonInteractive = $true; UseWindowsAuthentication = $UseWindowsAuthentication
        TrustLocalSqlCertificate = $TrustLocalSqlCertificate; SkipRetention = $SkipRetention
    }
    if ($EvidenceRoot) { $arguments.EvidenceRoot = $EvidenceRoot }
    & (Join-Path $PSScriptRoot 'Backup-Bitacora.ps1') @arguments
} catch {
    $backupFailed = $true
    Write-Host 'Respaldo programado detenido. Revisar el log; no se muestran configuracion ni credenciales.'
} finally {
    # A pipe disposal error can occur after STOP was delivered. Wait for that
    # same process before deciding whether a restart is needed in all paths.
    if ($restartRequired -and $null -ne $snapshot) {
        $stopDeadline = (Get-Date).AddSeconds(120)
        while ((Get-Process -Id $snapshot.ProcessId -ErrorAction SilentlyContinue) -and (Get-Date) -lt $stopDeadline) { Start-Sleep -Seconds 1 }
        if (Get-Process -Id $snapshot.ProcessId -ErrorAction SilentlyContinue) {
            $backupFailed = $true
            Write-Host 'La instancia original sigue activa; no se fuerza su cierre ni se inicia otra instancia.'
        }
    }
    if ($restartRequired -and $null -ne $snapshot -and -not (Get-NetTCPConnection -LocalPort $ApplicationPort -State Listen -ErrorAction SilentlyContinue)) {
        $previous = @{}
        try {
            foreach ($property in $snapshot.Environment.PSObject.Properties) {
                if ($property.Name -notmatch '^(ASPNETCORE_|DOTNET_|ConnectionStrings__|Storage__|Security__|Startup__|Logging__|AuditArchive__|BITACORA_)') { throw 'Variable de reinicio no admitida.' }
                $previous[$property.Name] = [Environment]::GetEnvironmentVariable($property.Name,'Process')
                [Environment]::SetEnvironmentVariable($property.Name,[string]$property.Value,'Process')
            }
            $stamp = (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [guid]::NewGuid().ToString('N')
            $startArguments = @{
                FilePath = $snapshot.Executable; WorkingDirectory = $snapshot.ContentRoot; WindowStyle = 'Hidden'; PassThru = $true
                RedirectStandardOutput = (Join-Path $ApplicationLogRoot ($stamp + '.out.log'))
                RedirectStandardError = (Join-Path $ApplicationLogRoot ($stamp + '.err.log'))
            }
            if ($snapshot.Arguments.Count -gt 0) { $startArguments.ArgumentList = '"' + $snapshot.Arguments[0] + '"' }
            $started = Start-Process @startArguments
            $deadline = (Get-Date).AddSeconds(120)
            do {
                $online = @(Get-NetTCPConnection -LocalPort $ApplicationPort -State Listen -ErrorAction SilentlyContinue)
                if ($online.Count -gt 0) { break }
                if ($started.HasExited) { throw 'La aplicacion termino durante el reinicio.' }
                Start-Sleep -Seconds 1
            } while ((Get-Date) -lt $deadline)
            if ($online.Count -eq 0 -or @($online | Where-Object OwningProcess -eq $started.Id).Count -eq 0) { throw 'El reinicio no recupero el puerto esperado.' }
            Write-Host 'Aplicacion reiniciada con la misma configuracion de proceso y puerto verificado.'
        } catch {
            $backupFailed = $true
            Write-Host 'FALLO DE REINICIO. Revisar los logs de ApplicationLogs y arrancar la version SQL con su configuracion habitual.'
        } finally {
            foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
            Remove-Variable snapshot -ErrorAction SilentlyContinue
        }
    }
}
if ($backupFailed) { throw 'El ciclo programado no termino correctamente. No se oculta el fallo a Task Scheduler.' }
