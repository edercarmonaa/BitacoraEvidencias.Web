param(
    [ValidateSet('SqlServer','Sqlite')][string]$Provider = 'SqlServer',
    [string]$BackupRoot = '',
    [ValidateRange(1,36500)][int]$RetentionDays = 30,
    [string]$ProjectRoot = '',
    [switch]$IncludeDevDatabase,
    [ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName = 'BitacoraEvidencias',
    [string]$EvidenceRoot = '',
    [string]$AuditArchiveRoot = '',
    [string]$TrialRoot = 'C:\BitacoraEvidencias_Pruebas',
    [string]$SqlBackupDirectory = $env:BITACORA_BACKUP_SQL_STAGING,
    [string]$SqlCredentialFile = $env:BITACORA_BACKUP_SQL_CREDENTIAL_FILE,
    [Management.Automation.PSCredential]$SqlCredential,
    [switch]$UseWindowsAuthentication,
    [switch]$TrustLocalSqlCertificate,
    [switch]$NonInteractive,
    [switch]$DatabaseOnly,
    [switch]$ApplicationStopped,
    [ValidateRange(0,65535)][int]$ApplicationPort = 0,
    [ValidateRange(30,86400)][int]$SqlTimeoutSeconds = 600,
    [switch]$SkipRetention
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Provider -eq 'Sqlite') {
    & (Join-Path $PSScriptRoot 'Backup-BitacoraSqlite.ps1') -BackupRoot $BackupRoot -RetentionDays $RetentionDays -ProjectRoot $ProjectRoot -IncludeDevDatabase:$IncludeDevDatabase
    return
}
if ($IncludeDevDatabase) { throw '-IncludeDevDatabase es exclusivo de -Provider Sqlite.' }
. (Join-Path $PSScriptRoot 'SqlBackupSupport.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ($ApplicationPort -eq 0) { $ApplicationPort = if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas') { 5068 } else { 5067 } }
if (-not $DatabaseOnly) {
    if (-not $ApplicationStopped) { throw 'El respaldo completo requiere detener todas las escrituras y usar -ApplicationStopped. -DatabaseOnly permite respaldar solo SQL en linea.' }
    Assert-BitacoraBackupStopped -Port $ApplicationPort
}
if (-not $BackupRoot) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT','Process') }
if (-not $BackupRoot) { $BackupRoot = [Environment]::GetEnvironmentVariable('BITACORA_BACKUP_ROOT','User') }
if (-not $BackupRoot) { $BackupRoot = 'C:\BitacoraEvidencias\BackupsBitacora' }
$baseRoot = Assert-BitacoraBackupLocalPath $BackupRoot
$destination = Join-Path $baseRoot ('SqlServer\' + $DatabaseName)
if (-not $EvidenceRoot) {
    $EvidenceRoot = if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas') { Join-Path $TrialRoot 'Input\Evidencias' } else { 'C:\BitacoraEvidencias\Evidencias' }
}
if (-not $DatabaseOnly) {
    $EvidenceRoot = Assert-BitacoraBackupLocalPath $EvidenceRoot
    if (-not (Test-Path -LiteralPath $EvidenceRoot -PathType Container)) { throw 'Falta la carpeta de evidencias; no se genera un respaldo completo.' }
    if ($destination.StartsWith($EvidenceRoot.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'El destino no puede estar dentro de las evidencias.' }
    if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas' -and
        ($EvidenceRoot.TrimEnd('\') -eq 'C:\BitacoraEvidencias' -or $EvidenceRoot.StartsWith('C:\BitacoraEvidencias\',[StringComparison]::OrdinalIgnoreCase))) {
        throw 'El ensayo debe usar su copia de evidencias, no las productivas.'
    }
    if (-not $AuditArchiveRoot) { $AuditArchiveRoot = if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas') { Join-Path $TrialRoot 'AuditArchive' } else { 'C:\BitacoraEvidencias\AuditArchive' } }
    $AuditArchiveRoot = Assert-BitacoraBackupLocalPath $AuditArchiveRoot
    if (-not (Test-Path -LiteralPath $AuditArchiveRoot -PathType Container)) { throw 'Falta la carpeta de historicos de auditoria. Revisar su ruta antes de considerar completo el respaldo.' }
    if ($DatabaseName -eq 'BitacoraEvidencias_Pruebas' -and $AuditArchiveRoot.StartsWith('C:\BitacoraEvidencias\',[StringComparison]::OrdinalIgnoreCase)) { throw 'El ensayo no respalda historicos productivos.' }
}

$name = 'BitacoraSqlBackup_{0}_{1}_{2}' -f $DatabaseName,(Get-Date -Format 'yyyyMMdd_HHmmss'),([guid]::NewGuid().ToString('N'))
$stagingParent = Assert-BitacoraBackupLocalPath ([IO.Path]::GetTempPath())
$staging = Assert-BitacoraBackupChild (Join-Path $stagingParent ('BSQL_' + [guid]::NewGuid().ToString('N'))) $stagingParent
$zipPath = Assert-BitacoraBackupChild (Join-Path $destination ($name + '.zip')) $destination
$partialZip = $zipPath + '.partial'
$logPath = Join-Path $destination ($name + '.log')
$connection = $null
$nativePath = $null
$complete = $false
$phase = 'credencial y conexion'

function Write-BackupLog {
    param([string]$Message)
    $line = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' | ' + $Message
    Write-Host $line
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
}

try {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Write-BackupLog ('Inicio SQL: ' + $DatabaseName + '; solo base: ' + [bool]$DatabaseOnly)
    if (-not $UseWindowsAuthentication -and $null -eq $SqlCredential) {
        if ($SqlCredentialFile) {
            $SqlCredential = Import-Clixml -LiteralPath $SqlCredentialFile
            if ($SqlCredential -isnot [Management.Automation.PSCredential]) { throw 'El archivo no contiene una credencial SQL protegida.' }
        } elseif ($NonInteractive) {
            throw 'Falta SqlCredentialFile para la tarea. Preparalo con Save-BitacoraSqlBackupCredential.ps1 usando la misma cuenta Windows de la tarea.'
        } else {
            [string]$sqlUser = Read-Host 'Usuario SQL para respaldos'
            $sqlPassword = Read-Host 'Contrasena SQL' -AsSecureString
            $SqlCredential = [Management.Automation.PSCredential]::new($sqlUser,$sqlPassword)
        }
    }
    $connection = New-BitacoraBackupConnection -DatabaseName $DatabaseName -Credential $SqlCredential -UseWindowsAuthentication:$UseWindowsAuthentication -TrustLocalSqlCertificate:$TrustLocalSqlCertificate
    $connection.Open()
    if (-not $DatabaseOnly) {
        # The SQL archiver uses the same resource exclusively. Hold this shared
        # session lock until packaging completes so exports/deletions cannot race.
        $archiveLock = $connection.CreateCommand()
        $archiveLock.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'BitacoraAuditArchive-0.2', @LockMode='Shared', @LockOwner='Session', @LockTimeout=0; SELECT @result;"
        try { if ([int]$archiveLock.ExecuteScalar() -lt 0) { throw 'Archivado de auditoria activo; reintentar fuera de ese horario.' } } finally { $archiveLock.Dispose() }
    }
    $phase = 'comprobar base migrada'
    $ready = New-BitacoraBackupCommand -Connection $connection -Operation Ready -DatabaseName $DatabaseName
    try { if ([int]$ready.ExecuteScalar() -lt 1) { throw 'La base no contiene la migracion SQL inicial. No se respalda una base vacia como si fuera la aplicacion migrada.' } } finally { $ready.Dispose() }
    if (-not $SqlBackupDirectory) {
        $pathCommand = New-BitacoraBackupCommand -Connection $connection -Operation DefaultPath -DatabaseName $DatabaseName
        try { $SqlBackupDirectory = [string]$pathCommand.ExecuteScalar() } finally { $pathCommand.Dispose() }
    }
    $SqlBackupDirectory = Assert-BitacoraBackupLocalPath $SqlBackupDirectory
    $nativePath = Assert-BitacoraBackupChild (Join-Path $SqlBackupDirectory ($name + '.bak')) $SqlBackupDirectory
    if (Test-Path -LiteralPath $nativePath) { throw 'El archivo nativo ya existe. No se sobrescribe.' }
    $phase = 'BACKUP DATABASE'
    $backup = New-BitacoraBackupCommand -Connection $connection -Operation Backup -DatabaseName $DatabaseName -BackupPath $nativePath -TimeoutSeconds $SqlTimeoutSeconds
    try { [void]$backup.ExecuteNonQuery() } finally { $backup.Dispose() }
    $phase = 'RESTORE VERIFYONLY'
    $verify = New-BitacoraBackupCommand -Connection $connection -Operation Verify -DatabaseName $DatabaseName -BackupPath $nativePath -TimeoutSeconds $SqlTimeoutSeconds
    try { [void]$verify.ExecuteNonQuery() } finally { $verify.Dispose() }
    $phase = 'RESTORE HEADERONLY'
    $headerCommand = New-BitacoraBackupCommand -Connection $connection -Operation Header -DatabaseName $DatabaseName -BackupPath $nativePath
    try {
        $header = $headerCommand.ExecuteReader()
        try {
            if (-not $header.Read() -or $header['DatabaseName'] -cne $DatabaseName -or [int]$header['BackupType'] -ne 1 -or
                -not [bool]$header['IsCopyOnly'] -or -not [bool]$header['HasBackupChecksums']) { throw 'La cabecera no corresponde al respaldo completo esperado.' }
            if ($header.Read()) { throw 'El archivo tiene mas de un conjunto de respaldo.' }
        } finally { $header.Dispose() }
    } finally { $headerCommand.Dispose() }
    Write-BackupLog 'BACKUP COPY_ONLY y RESTORE VERIFYONLY con CHECKSUM aprobados.'
    $phase = 'copiar BAK a staging; revisar permisos de lectura de la cuenta Windows'
    New-BitacoraBackupPrivateStaging -Path $staging
    New-Item -ItemType Directory -Path (Join-Path $staging 'Data') | Out-Null
    $bakCopy = Join-Path $staging ('Data\' + $DatabaseName + '.bak')
    Copy-Item -LiteralPath $nativePath -Destination $bakCopy
    $bakHash = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $bakCopy -Algorithm SHA256).Hash -cne $bakHash) { throw 'La copia BAK no coincide.' }
    $files = [Collections.Generic.List[object]]::new()
    $files.Add([ordered]@{ path = 'Data/' + $DatabaseName + '.bak'; sha256 = $bakHash; bytes = (Get-Item -LiteralPath $bakCopy).Length })
    if (-not $DatabaseOnly) {
        $phase = 'copiar y comparar evidencias'
        Assert-BitacoraBackupStopped -Port $ApplicationPort
        $evidenceDest = Join-Path $staging 'Evidencias'
        New-Item -ItemType Directory -Path $evidenceDest | Out-Null
        $sourceFiles = @(Get-BitacoraBackupEvidenceFiles -Root $EvidenceRoot)
        $copied = 0
        foreach ($file in $sourceFiles) {
            $relative = $file.FullName.Substring($EvidenceRoot.TrimEnd('\').Length + 1)
            $target = Assert-BitacoraBackupChild (Join-Path $evidenceDest $relative) $evidenceDest
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            Copy-Item -LiteralPath $file.FullName -Destination $target
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $hash) { throw 'Una evidencia cambio durante la copia.' }
            $files.Add([ordered]@{ path = 'Evidencias/' + $relative.Replace('\','/'); sha256 = $hash; bytes = (Get-Item -LiteralPath $target).Length })
            $copied++
            if ($copied % 500 -eq 0) { Write-BackupLog ("Evidencias copiadas: $copied de " + $sourceFiles.Count) }
        }
        $after = @(Get-BitacoraBackupEvidenceFiles -Root $EvidenceRoot)
        if ($after.Count -ne $sourceFiles.Count) { throw 'Las evidencias cambiaron durante el respaldo.' }
        foreach ($item in $files | Where-Object { $_.path.StartsWith('Evidencias/') }) {
            $source = Join-Path $EvidenceRoot $item.path.Substring(11).Replace('/','\')
            if (-not (Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne $item.sha256) {
                throw 'Las evidencias cambiaron durante el respaldo.'
            }
        }
        Assert-BitacoraBackupStopped -Port $ApplicationPort
        Write-BackupLog ('Evidencias copiadas y comparadas por SHA256: ' + $sourceFiles.Count)
        $phase = 'copiar historicos de auditoria'
        New-Item -ItemType Directory -Path (Join-Path $staging 'AuditArchive') | Out-Null
        $histories = @(Get-BitacoraAuditHistoryFiles -Root $AuditArchiveRoot -DatabaseName $DatabaseName)
        foreach ($history in $histories) {
            $relative = $history.FullName.Substring($AuditArchiveRoot.TrimEnd('\').Length + 1)
            $target = Assert-BitacoraBackupChild (Join-Path $staging ('AuditArchive\' + $relative)) (Join-Path $staging 'AuditArchive')
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
            $hash = (Get-FileHash -LiteralPath $history.FullName -Algorithm SHA256).Hash
            Copy-Item -LiteralPath $history.FullName -Destination $target
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $hash) { throw 'Un historico cambio durante el respaldo.' }
            $files.Add([ordered]@{ path = 'AuditArchive/' + $relative.Replace('\','/'); sha256 = $hash; bytes = (Get-Item -LiteralPath $target).Length })
        }
        Write-BackupLog ('Archivos historicos de auditoria incluidos: ' + $histories.Count)
    }
    $manifest = [ordered]@{
        formatVersion = 1; provider = 'SqlServer'; database = $DatabaseName; server = '.\Auditoria'
        createdAtUtc = [datetime]::UtcNow.ToString('o'); copyOnly = $true; backupChecksums = $true; verifyOnlyPassed = $true
        applicationWritesStopped = [bool](-not $DatabaseOnly); evidenceIncluded = [bool](-not $DatabaseOnly)
        auditArchiveIncluded = [bool](-not $DatabaseOnly)
        secretsIncluded = $false; files = @($files.ToArray())
        recoveryRequires = @('Compatible SQL Server; RESTORE and DBCC CHECKDB rehearsal', 'External protected copy of BITACORA_DATA_PROTECTION_KEY and SQL application credentials', 'External HTTPS certificate and its password; application configuration and binaries')
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $staging 'backup-manifest.json') -Encoding UTF8
    # ZipFile streams entries and supports Zip64, unlike Compress-Archive's per-file limit.
    $phase = 'crear y verificar ZIP'
    Write-BitacoraBackupArchive -SourceRoot $staging -DestinationPath $partialZip
    $archive = [IO.Compression.ZipFile]::OpenRead($partialZip)
    try {
        foreach ($item in $files) {
            $entry = $archive.GetEntry($item.path)
            if ($null -eq $entry -or $entry.Length -ne $item.bytes) { throw 'El ZIP no contiene todos los archivos esperados.' }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -cne $item.sha256) { throw 'La verificacion SHA256 del ZIP fallo.' }
        }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath $partialZip -Destination $zipPath
    $complete = $true
    Write-BackupLog ('Respaldo SQL terminado: ' + $zipPath)
    if (-not $SkipRetention) {
        foreach ($old in @(Get-BitacoraExpiredSqlBackups -Root $destination -DatabaseName $DatabaseName -Cutoff (Get-Date).AddDays(-$RetentionDays))) {
            $safeOld = Assert-BitacoraBackupChild $old.FullName $destination
            Remove-Item -LiteralPath $safeOld
        }
    }
} catch {
    $errorDetail = $_.Exception
    while ($null -ne $errorDetail.InnerException) { $errorDetail = $errorDetail.InnerException }
    $errorCode = if ($errorDetail -is [Data.SqlClient.SqlException]) { 'SQL ' + $errorDetail.Number } else { $errorDetail.GetType().Name }
    if (Test-Path -LiteralPath $destination) {
        $state = if ($complete) { 'El archivo nuevo esta verificado; fallo la retencion posterior.' } else { 'No se considera respaldo completo; no se aplica retencion.' }
        Write-BackupLog ('ERROR (' + $phase + '): ' + $errorCode + '. ' + $state)
    }
    throw 'Respaldo SQL detenido. Revisar permisos SQL/archivos, credencial y ventana de mantenimiento. No se imprimen mensajes del proveedor ni secretos.'
} finally {
    if ($null -ne $connection) { $connection.Dispose() }
    # Keep a verified native backup if packaging fails. Delete only our exact file after success.
    if ($complete -and $nativePath -and (Test-Path -LiteralPath $nativePath)) {
        $safeNative = Assert-BitacoraBackupChild $nativePath $SqlBackupDirectory
        try { Remove-Item -LiteralPath $safeNative -ErrorAction Stop } catch { Write-BackupLog 'Aviso: se conserva tambien el BAK nativo por permisos de limpieza.' }
    }
    if (Test-Path -LiteralPath $staging) {
        $safeStaging = Assert-BitacoraBackupChild $staging $stagingParent
        $links = @(Get-ChildItem -LiteralPath $safeStaging -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
        if ($links.Count -eq 0) { Remove-Item -LiteralPath $safeStaging -Recurse -Force }
    }
    if (Test-Path -LiteralPath $partialZip) { Remove-Item -LiteralPath (Assert-BitacoraBackupChild $partialZip $destination) }
    Remove-Variable SqlCredential,sqlPassword -ErrorAction SilentlyContinue
}
