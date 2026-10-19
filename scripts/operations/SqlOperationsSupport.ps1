Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '..\backup\SqlBackupSupport.ps1')

function Open-BitacoraOperationsConnection {
    param([ValidateSet('BitacoraEvidencias','BitacoraEvidencias_Pruebas')][string]$DatabaseName,
        [string]$SqlCredentialFile, [Management.Automation.PSCredential]$Credential,
        [switch]$UseWindowsAuthentication, [switch]$TrustLocalSqlCertificate, [switch]$NonInteractive)
    if (-not $UseWindowsAuthentication -and $null -eq $Credential) {
        if ($SqlCredentialFile) {
            $Credential = Import-Clixml -LiteralPath $SqlCredentialFile
            if ($Credential -isnot [Management.Automation.PSCredential]) { throw 'Credencial protegida no valida.' }
        } elseif ($NonInteractive) { throw 'Falta la credencial SQL protegida para la tarea.' }
        else {
            [string]$sqlUser = Read-Host 'Usuario SQL de operaciones'
            $password = Read-Host 'Contrasena SQL' -AsSecureString
            $Credential = [Management.Automation.PSCredential]::new($sqlUser,$password)
        }
    }
    $connection = New-BitacoraBackupConnection -DatabaseName $DatabaseName -Credential $Credential -UseWindowsAuthentication:$UseWindowsAuthentication -TrustLocalSqlCertificate:$TrustLocalSqlCertificate
    try {
        $connection.Open()
        $command = New-BitacoraBackupCommand -Connection $connection -DatabaseName $DatabaseName -Operation Ready
        try { if ([int]$command.ExecuteScalar() -lt 1) { throw 'Falta la migracion inicial SQL.' } } finally { $command.Dispose() }
        return $connection
    } catch { $connection.Dispose(); throw 'No se pudo validar la conexion SQL de operaciones. Revisar credencial, TLS, permisos y esquema; no se muestran datos del proveedor.' }
}

function New-BitacoraOperationsCommand {
    param([Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{}, [Data.SqlClient.SqlTransaction]$Transaction)
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    $command.CommandTimeout = 30
    if ($null -ne $Transaction) { $command.Transaction = $Transaction }
    foreach ($name in $Parameters.Keys) { [void]$command.Parameters.AddWithValue($name,$Parameters[$name]) }
    return $command
}

function Invoke-BitacoraOperationsTable {
    param([Data.SqlClient.SqlConnection]$Connection, [string]$Sql, [hashtable]$Parameters = @{})
    $command = New-BitacoraOperationsCommand $Connection $Sql $Parameters
    try {
        $reader = $command.ExecuteReader()
        try { $table = [Data.DataTable]::new(); $table.Load($reader); return ,$table } finally { $reader.Dispose() }
    } finally { $command.Dispose() }
}

function Write-BitacoraOperationsJson {
    param([string]$Path, [object]$Value)
    $partial = $Path + '.' + [guid]::NewGuid().ToString('N') + '.partial'
    try {
        [IO.File]::WriteAllText($partial,($Value | ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $Path) { [IO.File]::Replace($partial,$Path,[Management.Automation.Language.NullString]::Value) }
        else { [IO.File]::Move($partial,$Path) }
    } finally { if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial } }
}

function Write-BitacoraAuditBatch {
    param([string]$Directory, [object[]]$Rows, [object[]]$Columns, [string]$DatabaseName, [datetime]$CutoffUtc)
    if ($Rows.Count -eq 0) { throw 'No se exportan lotes vacios.' }
    $path = Assert-BitacoraBackupChild (Join-Path $Directory ('AuditLog_' + [datetime]::UtcNow.ToString('yyyyMMddTHHmmssfffffff') + '_' + [guid]::NewGuid().ToString('N') + '.jsonl')) $Directory
    $partial = $path + '.partial'
    $writer = [IO.StreamWriter]::new([IO.File]::Open($partial,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None),[Text.UTF8Encoding]::new($false))
    try { foreach ($row in $Rows) { $writer.WriteLine(($row | ConvertTo-Json -Depth 8 -Compress)) }; $writer.Flush(); $writer.BaseStream.Flush($true) } finally { $writer.Dispose() }
    $expectedHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash
    $lines = [IO.File]::ReadAllLines($partial,[Text.Encoding]::UTF8)
    if ($lines.Count -ne $Rows.Count) { throw 'El archivo no contiene todas las filas.' }
    for ($i = 0; $i -lt $Rows.Count; $i++) {
        # Compare every serialized value, including nulls and protected columns.
        if ($lines[$i] -cne ($Rows[$i] | ConvertTo-Json -Depth 8 -Compress)) { throw 'La exportacion no coincide con el lote SQL.' }
    }
    [IO.File]::Move($partial,$path)
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $expectedHash) { throw 'La huella del archivo publicado no coincide.' }
    $manifest = [ordered]@{ formatVersion = 1; provider = 'SqlServer'; database = $DatabaseName; file = [IO.Path]::GetFileName($path)
        sha256 = $expectedHash; rows = $Rows.Count; columns = @($Columns); cutoffUtc = $CutoffUtc.ToString('o')
        createdAtUtc = [datetime]::UtcNow.ToString('o'); deletionRequested = $false; deletionCommitted = $false }
    Write-BitacoraOperationsJson -Path ($path + '.manifest.json') -Value $manifest
    return [pscustomobject]@{ Path = $path; Manifest = $manifest }
}

function Invoke-BitacoraAuditArchive {
    param([Data.SqlClient.SqlConnection]$Connection, [string]$Directory, [datetime]$CutoffUtc,
        [ValidateSet('Preview','Export','Archive')][string]$Mode = 'Preview', [ValidateRange(1,500)][int]$BatchSize = 200)
    $counts = Invoke-BitacoraOperationsTable $Connection 'SELECT COUNT_BIG(*) AS Total, COALESCE(MAX(Id),0) AS MaximumId FROM dbo.AuditLogs WHERE OccurredAtUtc < @cutoff;' @{ '@cutoff' = $CutoffUtc }
    $result = [ordered]@{ eligibleRows = [long]$counts.Rows[0].Total; exportedRows = 0L; deletedRows = 0L; files = 0; cutoffUtc = $CutoffUtc.ToString('o'); mode = $Mode }
    if ($Mode -eq 'Preview') { return $result }
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $lock = [IO.File]::Open((Join-Path $Directory 'archive.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $lockAcquired = $false
    try {
        # Serialize archivers even when a caller uses a different destination.
        $appLock = New-BitacoraOperationsCommand $Connection "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'BitacoraAuditArchive-0.2', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @result;"
        try { if ([int]$appLock.ExecuteScalar() -lt 0) { throw 'Ya existe un archivado activo.' }; $lockAcquired = $true } finally { $appLock.Dispose() }
        $lastId = 0L
        while ($true) {
            $transaction = $Connection.BeginTransaction([Data.IsolationLevel]::RepeatableRead)
            try {
                $hint = if ($Mode -eq 'Archive') { ' WITH (UPDLOCK)' } else { '' }
                $command = New-BitacoraOperationsCommand $Connection ("SELECT TOP (@batch) * FROM dbo.AuditLogs" + $hint + " WHERE Id > @last AND Id <= @maximum AND OccurredAtUtc < @cutoff ORDER BY Id;") @{
                    '@batch' = $BatchSize; '@last' = $lastId; '@maximum' = [long]$counts.Rows[0].MaximumId; '@cutoff' = $CutoffUtc } $transaction
                $rows = [Collections.Generic.List[object]]::new(); $columns = @()
                try {
                    $reader = $command.ExecuteReader()
                    try {
                        for ($i = 0; $i -lt $reader.FieldCount; $i++) { $columns += [ordered]@{ name = $reader.GetName($i); sqlType = $reader.GetDataTypeName($i) } }
                        while ($reader.Read()) {
                            $row = [ordered]@{}
                            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                                $value = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
                                if ($value -is [datetime]) { $value = [datetime]::SpecifyKind($value,[DateTimeKind]::Utc).ToString('o') }
                                $row[$reader.GetName($i)] = $value
                            }
                            $rows.Add($row)
                        }
                    } finally { $reader.Dispose() }
                } finally { $command.Dispose() }
                if ($rows.Count -eq 0) { $transaction.Commit(); break }
                $batch = Write-BitacoraAuditBatch -Directory $Directory -Rows $rows.ToArray() -Columns $columns -DatabaseName $Connection.Database -CutoffUtc $CutoffUtc
                if ($Mode -eq 'Archive') {
                    $batch.Manifest.deletionRequested = $true
                    Write-BitacoraOperationsJson ($batch.Path + '.manifest.json') $batch.Manifest
                    $parameters = @{}; $names = @()
                    for ($i = 0; $i -lt $rows.Count; $i++) { $name = '@id' + $i; $parameters[$name] = [long]$rows[$i].Id; $names += $name }
                    $delete = New-BitacoraOperationsCommand $Connection ('DELETE FROM dbo.AuditLogs WHERE Id IN (' + ($names -join ',') + ');') $parameters $transaction
                    try { $deleted = $delete.ExecuteNonQuery() } finally { $delete.Dispose() }
                    if ($deleted -ne $rows.Count) { throw 'El numero eliminado no coincide con el lote verificado.' }
                }
                $transaction.Commit()
                if ($Mode -eq 'Archive') {
                    $result.deletedRows += $rows.Count
                    $batch.Manifest.deletionCommitted = $true
                    Write-BitacoraOperationsJson ($batch.Path + '.manifest.json') $batch.Manifest
                }
                $lastId = [long]$rows[$rows.Count - 1].Id
                $result.exportedRows += $rows.Count; $result.files++
            } catch { try { $transaction.Rollback() } catch {}; throw } finally { $transaction.Dispose() }
        }
    } finally {
        try {
            if ($lockAcquired) {
                $release = New-BitacoraOperationsCommand $Connection "EXEC sys.sp_releaseapplock @Resource=N'BitacoraAuditArchive-0.2', @LockOwner='Session';"
                try { [void]$release.ExecuteNonQuery() } finally { $release.Dispose() }
            }
        } finally { $lock.Dispose() }
    }
    return $result
}

function Test-BitacoraSqlBackupPackage {
    param([string]$Path, [string]$DatabaseName)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = $null
    try {
        $archive = [IO.Compression.ZipFile]::OpenRead($Path)
        $entry = $archive.GetEntry('backup-manifest.json')
        if ($null -eq $entry -or $entry.Length -gt 16MB) { return $false }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if (@($manifest.PSObject.Properties.Name) -notcontains 'auditArchiveIncluded') { return $false }
        if ($manifest.formatVersion -ne 1 -or $manifest.provider -cne 'SqlServer' -or $manifest.database -cne $DatabaseName -or
            $manifest.verifyOnlyPassed -ne $true -or $manifest.backupChecksums -ne $true -or $manifest.evidenceIncluded -ne $true -or
            $manifest.auditArchiveIncluded -ne $true -or $manifest.applicationWritesStopped -ne $true -or $manifest.copyOnly -ne $true) { return $false }
        if ($null -eq $archive.GetEntry('Evidencias/') -or $null -eq $archive.GetEntry('AuditArchive/')) { return $false }
        $baks = @($manifest.files | Where-Object path -ceq ('Data/' + $DatabaseName + '.bak'))
        if ($baks.Count -ne 1) { return $false }
        foreach ($file in $manifest.files) {
            $item = $archive.GetEntry($file.path)
            if ($null -eq $item -or $item.Length -ne $file.bytes -or $file.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { return $false }
        }
        return $true
    } catch { return $false } finally { if ($null -ne $archive) { $archive.Dispose() } }
}
