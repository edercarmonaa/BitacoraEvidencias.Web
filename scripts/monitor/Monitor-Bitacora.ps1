param(
    [ValidateSet('SqlServer','Sqlite')][string]$Provider = 'SqlServer',
    [string]$MonitorRoot = '', [string]$AppUrl = '', [string]$BackupRoot = '',
    [string]$DataRoot = 'C:\BitacoraEvidencias\Data', [string]$EvidenceRoot = '',
    [string]$DatabasePath = 'C:\BitacoraEvidencias\Data\bitacora-evidencias.db',
    [ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName = 'BitacoraEvidencias',
    [ValidateRange(0,100000)][double]$MinFreeDiskGb = 25,
    [ValidateRange(0.001,100000)][double]$MaxEvidenceGb = 50,
    [ValidateRange(0.001,100000)][double]$MaxDatabaseGb = 5,
    [ValidateRange(1,8760)][int]$BackupMaxAgeHours = 26,
    [ValidateRange(1,8760)][int]$CriticalEventHours = 24,
    [ValidateRange(1,300)][int]$AppRequestTimeoutSec = 15,
    [string]$SqlCredentialFile = $env:BITACORA_OPERATIONS_SQL_CREDENTIAL_FILE,
    [Management.Automation.PSCredential]$SqlCredential,
    [switch]$UseWindowsAuthentication, [switch]$TrustLocalSqlCertificate, [switch]$NonInteractive
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Provider -eq 'Sqlite') {
    if (-not $MonitorRoot) { $MonitorRoot = 'C:\BitacoraEvidencias\Monitoreo' }
    if (-not $AppUrl) { $AppUrl = 'https://127.0.0.1:5067/Login' }
    if (-not $EvidenceRoot) { $EvidenceRoot = 'C:\BitacoraEvidencias\Evidencias' }
    & (Join-Path $PSScriptRoot 'Monitor-BitacoraSqlite.ps1') -MonitorRoot $MonitorRoot -AppUrl $AppUrl -DataRoot $DataRoot -EvidenceRoot $EvidenceRoot -DatabasePath $DatabasePath -MinFreeDiskGb $MinFreeDiskGb -MaxEvidenceGb $MaxEvidenceGb -MaxDatabaseGb $MaxDatabaseGb -BackupMaxAgeHours $BackupMaxAgeHours -CriticalEventHours $CriticalEventHours -AppRequestTimeoutSec $AppRequestTimeoutSec
    return
}
. (Join-Path $PSScriptRoot '..\operations\SqlOperationsSupport.ps1')
$trial = $DatabaseName -eq 'BitacoraEvidencias_Pruebas'
$installation = if ($trial) { 'C:\BitacoraEvidencias_Pruebas' } else { 'C:\BitacoraEvidencias' }
if (-not $MonitorRoot) { $MonitorRoot = Join-Path $installation 'Monitoreo' }
if (-not $EvidenceRoot) { $EvidenceRoot = if ($trial) { Join-Path $installation 'Input\Evidencias' } else { Join-Path $installation 'Evidencias' } }
if (-not $AppUrl) { $AppUrl = if ($trial) { 'https://127.0.0.1:5068/Login' } else { 'https://127.0.0.1:5067/Login' } }
$uri = $null
if (-not [uri]::TryCreate($AppUrl,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -notin @('https','http') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) { throw 'Usa una URL de disponibilidad sin credenciales, parametros ni fragmento.' }
$MonitorRoot = Assert-BitacoraBackupLocalPath $MonitorRoot
$EvidenceRoot = Assert-BitacoraBackupLocalPath $EvidenceRoot
if ($trial -and $MonitorRoot.StartsWith('C:\BitacoraEvidencias\',[StringComparison]::OrdinalIgnoreCase)) { throw 'El ensayo no escribe reportes productivos.' }
if (-not $BackupRoot) {
    foreach ($scope in @('Process','User','Machine')) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT',$scope); if ($BackupRoot) { break } }
}
if (-not $BackupRoot) { $BackupRoot = 'C:\BitacoraEvidencias\BackupsBitacora' }
$BackupRoot = Assert-BitacoraBackupLocalPath $BackupRoot
$destination = Join-Path $MonitorRoot ('SqlServer\' + $DatabaseName)
New-Item -ItemType Directory -Path (Join-Path $destination 'status') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $destination 'logs') -Force | Out-Null
$stamp = (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [guid]::NewGuid().ToString('N')
$script:monitorLog = Join-Path $destination ('logs\Monitor_' + (Get-Date -Format 'yyyyMMdd') + '.log')
$script:monitorChecks = [Collections.Generic.List[object]]::new()
$summary = [ordered]@{ provider = 'SqlServer'; database = $DatabaseName; appUrl = $AppUrl; startedAtUtc = [datetime]::UtcNow.ToString('o'); overallStatus = 'RUNNING'; checks = @(); criticalEvents = @{} }
Write-BitacoraOperationsJson (Join-Path $destination 'latest-run.json') $summary
function Invoke-SqlMonitorCheck([string]$Name,[scriptblock]$Action) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try { $result = & $Action; $status = $result.status; $details = $result.details }
    catch { $status = 'ALERT'; $details = 'No se pudo comprobar. Revisar disponibilidad, permisos y configuracion; no se muestran datos del proveedor.' }
    $watch.Stop()
    $script:monitorChecks.Add([ordered]@{ name = $Name; status = $status; details = $details; durationMs = $watch.ElapsedMilliseconds })
    $message = $Name + ' | ' + $status + ' | ' + $details
    Write-Host $message
    Add-Content -LiteralPath $script:monitorLog -Value ([datetime]::UtcNow.ToString('o') + ' | ' + $message)
}
$connection = $null
try {
    Invoke-SqlMonitorCheck 'AppAvailability' {
        $response = Invoke-WebRequest -Uri $AppUrl -Method Get -TimeoutSec $AppRequestTimeoutSec -UseBasicParsing
        @{ status = if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) { 'OK' } else { 'ALERT' }; details = 'HTTP ' + $response.StatusCode }
    }
    Invoke-SqlMonitorCheck 'SqlConnectivity' {
        $script:connection = Open-BitacoraOperationsConnection -DatabaseName $DatabaseName -SqlCredentialFile $SqlCredentialFile -Credential $SqlCredential -UseWindowsAuthentication:$UseWindowsAuthentication -TrustLocalSqlCertificate:$TrustLocalSqlCertificate -NonInteractive:$NonInteractive
        @{ status = 'OK'; details = 'Conexion cifrada y migracion SQL inicial comprobadas.' }
    }
    Invoke-SqlMonitorCheck 'RecentBackup' {
        $backupDirectory = Join-Path $BackupRoot ('SqlServer\' + $DatabaseName)
        $pattern = '^BitacoraSqlBackup_' + [regex]::Escape($DatabaseName) + '_[0-9]{8}_[0-9]{6}_[a-f0-9]{32}\.zip$'
        $latest = Get-ChildItem -LiteralPath $backupDirectory -File -Filter '*.zip' | Where-Object { $_.Name -match $pattern -and -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($null -eq $latest -or $latest.LastWriteTimeUtc -lt [datetime]::UtcNow.AddHours(-$BackupMaxAgeHours)) { return @{ status = 'ALERT'; details = 'No hay respaldo SQL completo reciente para esta base.' } }
        if (-not (Test-BitacoraSqlBackupPackage -Path $latest.FullName -DatabaseName $DatabaseName)) { return @{ status = 'ALERT'; details = 'ZIP reciente incompleto o manifiesto SQL no valido.' } }
        @{ status = 'OK'; details = 'Respaldo SQL completo reciente; manifiesto y entradas presentes. No se recalculan hashes durante el monitoreo.' }
    }
    foreach ($volume in @('C',([IO.Path]::GetPathRoot($BackupRoot).Substring(0,1))) | Select-Object -Unique) {
        Invoke-SqlMonitorCheck ('DiskFree' + $volume) {
            $free = [math]::Round((Get-PSDrive -Name $volume).Free / 1GB,2)
            @{ status = if ($free -ge $MinFreeDiskGb) { 'OK' } else { 'ALERT' }; details = "$free GB libres; umbral $MinFreeDiskGb GB." }
        }
    }
    Invoke-SqlMonitorCheck 'EvidenceSize' {
        if (-not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)) { throw 'Falta carpeta de evidencias.' }
        $bytes = 0L; foreach ($file in Get-BitacoraBackupEvidenceFiles -Root $EvidenceRoot) { $bytes += $file.Length }
        $gb = [math]::Round($bytes / 1GB,3)
        @{ status = if ($bytes -le $MaxEvidenceGb * 1GB) { 'OK' } else { 'ALERT' }; details = "$gb GB; umbral $MaxEvidenceGb GB." }
    }
    Invoke-SqlMonitorCheck 'DatabaseSize' {
        if ($null -eq $connection) { throw 'Conexion SQL no disponible.' }
        $sizes = Invoke-BitacoraOperationsTable $connection 'SELECT COALESCE(SUM(CASE WHEN type=0 THEN CONVERT(bigint,size)*8192 ELSE 0 END),0) AS DataBytes, COALESCE(SUM(CASE WHEN type=1 THEN CONVERT(bigint,size)*8192 ELSE 0 END),0) AS LogBytes FROM sys.database_files;'
        $dataGb = [math]::Round([double]$sizes.Rows[0].DataBytes / 1GB,3); $logGb = [math]::Round([double]$sizes.Rows[0].LogBytes / 1GB,3)
        @{ status = if ([double]$sizes.Rows[0].DataBytes -le $MaxDatabaseGb * 1GB) { 'OK' } else { 'ALERT' }; details = "Datos SQL $dataGb GB; log $logGb GB; umbral de datos $MaxDatabaseGb GB." }
    }
    Invoke-SqlMonitorCheck 'CriticalEventsQuery' {
        if ($null -eq $connection) { throw 'Conexion SQL no disponible.' }
        $counts = [ordered]@{ LOGIN_FAIL = 0L; LOCKOUT = 0L; DELETE = 0L; DELETE_EVIDENCE = 0L }
        $table = Invoke-BitacoraOperationsTable $connection "SELECT EventType, COUNT_BIG(*) AS Total FROM dbo.AuditLogs WHERE OccurredAtUtc >= @cutoff AND EventType IN ('LOGIN_FAIL','LOCKOUT','DELETE','DELETE_EVIDENCE') GROUP BY EventType;" @{ '@cutoff' = [datetime]::UtcNow.AddHours(-$CriticalEventHours) }
        foreach ($row in $table.Rows) { $counts[[string]$row.EventType] = [long]$row.Total }
        $summary.criticalEvents = $counts
        @{ status = 'OK'; details = 'Conteos agregados de las ultimas ' + $CriticalEventHours + ' horas: ' + ($counts | ConvertTo-Json -Compress) }
    }
    $summary.checks = $script:monitorChecks.ToArray()
    $summary.overallStatus = if (@($script:monitorChecks | Where-Object status -eq 'ALERT').Count -gt 0) { 'ALERT' } else { 'OK' }
} finally {
    if ($null -ne $connection) { $connection.Dispose() }
    $summary.completedAtUtc = [datetime]::UtcNow.ToString('o')
    Write-BitacoraOperationsJson (Join-Path $destination ('status\MonitorStatus_' + $stamp + '.json')) $summary
    Write-BitacoraOperationsJson (Join-Path $destination 'latest-status.json') $summary
    Write-BitacoraOperationsJson (Join-Path $destination 'latest-run.json') $summary
    Remove-Variable SqlCredential -ErrorAction SilentlyContinue
}
if ($summary.overallStatus -ne 'OK') { throw 'Monitoreo SQL termino con alertas. Consultar latest-status.json; no se modifico la aplicacion ni la base.' }
