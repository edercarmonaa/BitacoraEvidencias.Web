param([switch]$Integration)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $repo 'scripts\operations\SqlOperationsSupport.ps1')
$script:checks = 0
function Assert-Operation([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message }; $script:checks++ }
foreach ($relative in @('operations\SqlOperationsSupport.ps1','operations\Register-BitacoraSqlOperationsTasks.ps1','audit\Archive-AuditLog.ps1','audit\Archive-AuditLogSqlite.ps1','monitor\Monitor-Bitacora.ps1','monitor\Monitor-BitacoraSqlite.ps1')) {
    $tokens = $null; $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $repo ('scripts\' + $relative)),[ref]$tokens,[ref]$errors)
    Assert-Operation ($errors.Count -eq 0) ('Sintaxis: ' + $relative)
}
$fixture = Join-Path $repo ('tmp\operations-tests\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$fake = [ordered]@{ Id = 5000000001L; OccurredAtUtc = '2020-01-01T00:00:00.0000000Z'; Curp = 'enc:v1:fake'; CurpHash = 'fake-hash'; CurpSuffix = 'FAKE'; Cct = $null; Details = "Unicode: a`nb; comillas=`""; Success = $false }
$batch = Write-BitacoraAuditBatch -Directory $fixture -Rows @($fake) -Columns @(@{ name = 'Id'; sqlType = 'bigint' }) -DatabaseName BitacoraEvidencias_Pruebas -CutoffUtc ([datetime]::UtcNow)
$row = Get-Content -LiteralPath $batch.Path -Raw | ConvertFrom-Json
Assert-Operation ($row.Id -eq 5000000001L -and $row.Curp -ceq $fake.Curp -and $row.Details -ceq $fake.Details) 'Valores alterados en JSONL.'
Assert-Operation ($null -eq $row.Cct -and $row.Success -eq $false -and $row.CurpHash -ceq 'fake-hash') 'Nulos, booleanos o hashes omitidos.'
Assert-Operation ((Get-FileHash -LiteralPath $batch.Path -Algorithm SHA256).Hash -ceq $batch.Manifest.sha256) 'Huella incorrecta.'
$second = Write-BitacoraAuditBatch -Directory $fixture -Rows @($fake) -Columns @() -DatabaseName BitacoraEvidencias_Pruebas -CutoffUtc ([datetime]::UtcNow)
Assert-Operation ($batch.Path -cne $second.Path -and (Test-Path -LiteralPath $batch.Path)) 'Se sobrescribio el archivo previo.'
$status = Join-Path $fixture 'status.json'
Write-BitacoraOperationsJson $status @{ value = 1 }
Write-BitacoraOperationsJson $status @{ value = 2 }
Assert-Operation ((Get-Content -LiteralPath $status -Raw | ConvertFrom-Json).value -eq 2) 'El estado no se actualizo atomicamente.'
New-Item -ItemType Directory -Path (Join-Path $fixture 'history\SqlServer\BitacoraEvidencias_Pruebas') -Force | Out-Null
foreach ($relative in @('history\AuditLog_20200101.log','history\unrelated.log',('history\SqlServer\BitacoraEvidencias_Pruebas\' + [IO.Path]::GetFileName($batch.Path)),('history\SqlServer\BitacoraEvidencias_Pruebas\' + [IO.Path]::GetFileName($batch.Path) + '.partial'))) { [IO.File]::WriteAllText((Join-Path $fixture $relative),'fake') }
$history = @(Get-BitacoraAuditHistoryFiles -Root (Join-Path $fixture 'history') -DatabaseName BitacoraEvidencias_Pruebas)
Assert-Operation ($history.Count -eq 2) 'El respaldo debe incluir historicos finalizados SQLite/SQL y excluir otros archivos.'
Assert-Operation (@(Get-BitacoraAuditHistoryFiles -Root (Join-Path $fixture 'history') -DatabaseName BitacoraEvidencias).Count -eq 1) 'Se incluyeron historicos de otra base SQL.'
Assert-Operation (-not (Test-BitacoraSqlBackupPackage -Path (Join-Path $fixture 'missing.zip') -DatabaseName BitacoraEvidencias_Pruebas)) 'Un ZIP ausente debe rechazarse.'
Assert-Operation (-not (Test-BitacoraSqlBackupPackage -Path $batch.Path -DatabaseName BitacoraEvidencias_Pruebas)) 'Un ZIP corrupto debe rechazarse.'

# Exercise task definitions with local mocks; never contact Task Scheduler.
if ([TimeZoneInfo]::Local.Id -in @('Central Standard Time (Mexico)','America/Mexico_City')) {
    $credentialPath = Join-Path $fixture 'fake.credential.xml'
    [Management.Automation.PSCredential]::new('fake-user',(ConvertTo-SecureString 'fake-password' -AsPlainText -Force)) | Export-Clixml -LiteralPath $credentialPath
    $global:bitacoraOperationsTestTasks = [Collections.Generic.List[object]]::new()
    function Get-ScheduledTask { param($TaskName,$ErrorAction) return $null }
    function New-ScheduledTaskPrincipal { param($UserId,$LogonType,$RunLevel) return @{ UserId=$UserId; LogonType=$LogonType; RunLevel=$RunLevel } }
    function New-ScheduledTaskAction { param($Execute,$Argument) return @{ Execute=$Execute; Argument=$Argument } }
    function New-ScheduledTaskSettingsSet { param($MultipleInstances,$ExecutionTimeLimit) return [pscustomobject]@{ Enabled=$true; StartWhenAvailable=$true; MultipleInstances=$MultipleInstances } }
    function New-ScheduledTaskTrigger { param([switch]$Daily,$At) return @{ At=$At } }
    function New-ScheduledTask { param($Action,$Trigger,$Principal,$Settings,$Description) return @{ Action=$Action; Trigger=$Trigger; Settings=$Settings; Principal=$Principal } }
    function Register-ScheduledTask { param($TaskName,$InputObject) $global:bitacoraOperationsTestTasks.Add(@{ Name=$TaskName; Task=$InputObject }) }
    & (Join-Path $repo 'scripts\operations\Register-BitacoraSqlOperationsTasks.ps1') -SqlCredentialFile $credentialPath -BackupRoot $fixture
    Assert-Operation ($global:bitacoraOperationsTestTasks.Count -eq 2) 'Se deben preparar dos tareas SQL.'
    Assert-Operation ($global:bitacoraOperationsTestTasks[0].Task.Trigger.At.ToString('HH:mm') -ceq '09:15' -and $global:bitacoraOperationsTestTasks[1].Task.Trigger.At.ToString('HH:mm') -ceq '09:20') 'Los horarios deben conservarse.'
    foreach ($item in $global:bitacoraOperationsTestTasks) { Assert-Operation (-not $item.Task.Settings.Enabled -and -not $item.Task.Settings.StartWhenAvailable -and $item.Name -match '-Sql') 'Las nuevas tareas deben estar deshabilitadas sin modificar SQLite.' }
    Assert-Operation ($global:bitacoraOperationsTestTasks[1].Task.Action.Argument -match '-Mode Archive -RetentionDays 30' -and $global:bitacoraOperationsTestTasks[0].Task.Action.Argument -notmatch 'fake-password') 'Politica o argumentos de tarea incorrectos.'
}

if ($Integration) {
    $name = 'BitacoraEvidencias_OperationsTest_' + [guid]::NewGuid().ToString('N')
    Assert-Operation ($name -match '^BitacoraEvidencias_OperationsTest_[a-f0-9]{32}$') 'Base temporal no valida.'
    $master = [Data.SqlClient.SqlConnection]::new('Server=.\Auditoria;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Pooling=False')
    $connection = $null; $created = $false
    try {
        $master.Open()
        $exists = Invoke-BitacoraOperationsTable $master 'SELECT DB_ID(@name) AS Id' @{ '@name' = $name }
        Assert-Operation ($exists.Rows[0].Id -is [DBNull]) 'La base temporal ya existe.'
        $command = New-BitacoraOperationsCommand $master ('CREATE DATABASE [' + $name + ']')
        try { [void]$command.ExecuteNonQuery(); $created = $true } finally { $command.Dispose() }
        $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new($master.ConnectionString); $builder.set_InitialCatalog($name)
        $connection = [Data.SqlClient.SqlConnection]::new($builder.ConnectionString); $connection.Open()
        $command = New-BitacoraOperationsCommand $connection "CREATE TABLE dbo.AuditLogs (Id bigint IDENTITY(5000000001,1) PRIMARY KEY, OccurredAtUtc datetime2 NOT NULL, EventType nvarchar(60) NOT NULL, Curp nvarchar(512) NULL, CurpHash nvarchar(64) NULL, CurpSuffix nvarchar(8) NULL, Cct nvarchar(512) NULL, Details nvarchar(2000) NULL, Success bit NOT NULL); INSERT dbo.AuditLogs(OccurredAtUtc,EventType,Curp,CurpHash,CurpSuffix,Cct,Details,Success) SELECT DATEADD(day,-60,SYSUTCDATETIME()),'LOGIN_FAIL','enc:v1:fake','fake-hash','FAKE',NULL,N'Unicode y nulos',0 FROM (VALUES(1),(2),(3),(4),(5)) AS x(n); INSERT dbo.AuditLogs(OccurredAtUtc,EventType,Success) VALUES(SYSUTCDATETIME(),'LOGIN',1);"
        try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
        $cutoff = [datetime]::UtcNow.AddDays(-30)
        $preview = Invoke-BitacoraAuditArchive $connection (Join-Path $fixture 'preview') $cutoff -Mode Preview
        Assert-Operation ($preview.eligibleRows -eq 5 -and $preview.deletedRows -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $fixture 'preview'))) 'Preview no debe escribir historicos ni retirar filas.'
        $export = Invoke-BitacoraAuditArchive $connection (Join-Path $fixture 'export') $cutoff -Mode Export -BatchSize 2
        Assert-Operation ($export.exportedRows -eq 5 -and $export.deletedRows -eq 0 -and $export.files -eq 3) 'Exportacion paginada incorrecta.'
        $count = Invoke-BitacoraOperationsTable $connection 'SELECT COUNT_BIG(*) AS Total FROM dbo.AuditLogs'
        Assert-Operation ($count.Rows[0].Total -eq 6) 'Export elimino registros.'
        $other = [Data.SqlClient.SqlConnection]::new($connection.ConnectionString)
        try {
            $other.Open()
            $command = New-BitacoraOperationsCommand $other "EXEC sys.sp_getapplock @Resource=N'BitacoraAuditArchive-0.2', @LockMode='Shared', @LockOwner='Session', @LockTimeout=0;"
            try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
            $blocked = $false
            try { Invoke-BitacoraAuditArchive $connection (Join-Path $fixture 'overlap') $cutoff -Mode Archive | Out-Null } catch { $blocked = $true }
            Assert-Operation $blocked 'Un respaldo con bloqueo compartido debe impedir el archivado.'
        } finally { $other.Dispose() }
        $command = New-BitacoraOperationsCommand $connection "CREATE TRIGGER dbo.BlockTestDelete ON dbo.AuditLogs AFTER DELETE AS BEGIN THROW 51000, 'Fallo de prueba', 1; END"
        try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
        $failed = $false
        try { Invoke-BitacoraAuditArchive $connection (Join-Path $fixture 'failure') $cutoff -Mode Archive -BatchSize 2 | Out-Null } catch { $failed = $true }
        $count = Invoke-BitacoraOperationsTable $connection 'SELECT COUNT_BIG(*) AS Total FROM dbo.AuditLogs'
        Assert-Operation ($failed -and $count.Rows[0].Total -eq 6) 'El fallo de DELETE debe revertir SQL y conservar todas las filas.'
        $command = New-BitacoraOperationsCommand $connection 'DROP TRIGGER dbo.BlockTestDelete'
        try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
        $archive = Invoke-BitacoraAuditArchive $connection (Join-Path $fixture 'archive') $cutoff -Mode Archive -BatchSize 2
        $count = Invoke-BitacoraOperationsTable $connection 'SELECT COUNT_BIG(*) AS Total FROM dbo.AuditLogs'
        Assert-Operation ($archive.exportedRows -eq 5 -and $archive.deletedRows -eq 5 -and $count.Rows[0].Total -eq 1) 'No coincide la retirada con los lotes verificados.'
        $again = Invoke-BitacoraAuditArchive $connection (Join-Path $fixture 'archive') $cutoff -Mode Archive -BatchSize 2
        Assert-Operation ($again.exportedRows -eq 0 -and $again.deletedRows -eq 0) 'La segunda ejecucion debe conservar lo ya archivado.'
        $archived = Get-ChildItem -LiteralPath (Join-Path $fixture 'archive') -Filter '*.jsonl'
        Assert-Operation ($archived.Count -eq 3) 'Se perdieron lotes historicos.'
        $restored = @(foreach ($file in $archived) { foreach ($line in [IO.File]::ReadAllLines($file.FullName)) { $line | ConvertFrom-Json } })
        Assert-Operation ($restored.Count -eq 5 -and @($restored | Where-Object { $_.Curp -ceq 'enc:v1:fake' -and $_.CurpHash -ceq 'fake-hash' -and $null -eq $_.Cct }).Count -eq 5) 'El historico debe conservar valores protegidos y columnas auxiliares.'
    } finally {
        if ($null -ne $connection) { $connection.Dispose() }
        if ($created) {
            $command = New-BitacoraOperationsCommand $master ('ALTER DATABASE [' + $name + '] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [' + $name + '];')
            try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
        }
        $master.Dispose()
    }
    Write-Host 'Integracion SQL: base temporal propia eliminada; produccion y ensayo conservados.'
}
Write-Host ('OK: ' + $script:checks + ' comprobaciones de operaciones SQL.')
