param(
    [ValidateSet('SqlServer','Sqlite')][string]$Provider = 'SqlServer',
    [string]$ArchiveRoot = '',
    [string]$DatabasePath = 'C:\BitacoraEvidencias\Data\bitacora-evidencias.db',
    [ValidateRange(1,36500)][int]$RetentionDays = 30,
    [ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName = 'BitacoraEvidencias',
    [ValidateSet('Preview','Export','Archive')][string]$Mode = 'Preview',
    [ValidateRange(1,500)][int]$BatchSize = 200,
    [string]$SqlCredentialFile = $env:BITACORA_OPERATIONS_SQL_CREDENTIAL_FILE,
    [Management.Automation.PSCredential]$SqlCredential,
    [switch]$UseWindowsAuthentication,
    [switch]$TrustLocalSqlCertificate,
    [switch]$NonInteractive
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Provider -eq 'Sqlite') {
    if (-not $ArchiveRoot) { $ArchiveRoot = 'C:\BitacoraEvidencias\AuditArchive' }
    & (Join-Path $PSScriptRoot 'Archive-AuditLogSqlite.ps1') -ArchiveRoot $ArchiveRoot -DatabasePath $DatabasePath -RetentionDays $RetentionDays
    return
}
. (Join-Path $PSScriptRoot '..\operations\SqlOperationsSupport.ps1')
if (-not $ArchiveRoot) { $ArchiveRoot = if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas') { 'C:\BitacoraEvidencias_Pruebas\AuditArchive' } else { 'C:\BitacoraEvidencias\AuditArchive' } }
$ArchiveRoot = Assert-BitacoraBackupLocalPath $ArchiveRoot
if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas' -and $ArchiveRoot.StartsWith('C:\BitacoraEvidencias\',[StringComparison]::OrdinalIgnoreCase)) { throw 'El ensayo no escribe historicos productivos.' }
$destination = Join-Path $ArchiveRoot ('SqlServer\' + $DatabaseName)
if (-not (Test-Path -LiteralPath $destination)) {
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    New-BitacoraBackupPrivateStaging -Path $destination
}
New-Item -ItemType Directory -Path (Join-Path $destination 'logs') -Force | Out-Null
$logPath = Join-Path $destination ('logs\ArchiveAudit_' + (Get-Date -Format 'yyyyMMdd') + '.log')
$state = [ordered]@{ status = 'RUNNING'; provider = 'SqlServer'; database = $DatabaseName; mode = $Mode
    retentionDays = $RetentionDays; startedAtUtc = [datetime]::UtcNow.ToString('o'); processId = $PID; result = $null }
$runPath = Join-Path $destination 'latest-run.json'
Write-BitacoraOperationsJson $runPath $state
$connection = $null
try {
    $connection = Open-BitacoraOperationsConnection -DatabaseName $DatabaseName -SqlCredentialFile $SqlCredentialFile -Credential $SqlCredential -UseWindowsAuthentication:$UseWindowsAuthentication -TrustLocalSqlCertificate:$TrustLocalSqlCertificate -NonInteractive:$NonInteractive
    $result = Invoke-BitacoraAuditArchive -Connection $connection -Directory $destination -CutoffUtc ([datetime]::UtcNow.AddDays(-$RetentionDays)) -Mode $Mode -BatchSize $BatchSize
    $state.status = 'COMPLETED'; $state.result = $result
    $message = 'Auditoria SQL: modo=' + $Mode + '; elegibles=' + $result.eligibleRows + '; exportadas=' + $result.exportedRows + '; retiradas de SQL=' + $result.deletedRows + '; archivos=' + $result.files
    Write-Host $message
    Add-Content -LiteralPath $logPath -Value ([datetime]::UtcNow.ToString('o') + ' | ' + $message)
} catch {
    $state.status = 'FAILED'
    Add-Content -LiteralPath $logPath -Value ([datetime]::UtcNow.ToString('o') + ' | FAILED: revisar conexion, permisos y archivos; no se imprimen mensajes del proveedor ni datos.')
    throw 'Archivado SQL detenido. Revisar latest-status.json y los archivos publicados; los lotes ya confirmados se conservan y no se sobrescriben. No se muestran datos ni credenciales.'
} finally {
    if ($null -ne $connection) { $connection.Dispose() }
    $state.completedAtUtc = [datetime]::UtcNow.ToString('o')
    Write-BitacoraOperationsJson $runPath $state
    Write-BitacoraOperationsJson (Join-Path $destination 'latest-status.json') $state
    if ($Mode -eq 'Archive') { Write-BitacoraOperationsJson (Join-Path $destination 'latest-archive-status.json') $state }
    Remove-Variable SqlCredential -ErrorAction SilentlyContinue
}
