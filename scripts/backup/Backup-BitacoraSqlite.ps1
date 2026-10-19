param(
    [string]$BackupRoot = "",
    [int]$RetentionDays = 30,
    [string]$ProjectRoot = "",
    [switch]$IncludeDevDatabase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Resolve-BackupRoot {
    param([string]$ConfiguredBackupRoot)

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredBackupRoot)) {
        return @{
            Path = $ConfiguredBackupRoot
            Source = "Parameter"
        }
    }

    $processBackupRoot = [Environment]::GetEnvironmentVariable("BITACORA_BACKUP_ROOT", "Process")
    if (-not [string]::IsNullOrWhiteSpace($processBackupRoot)) {
        return @{
            Path = $processBackupRoot
            Source = "Environment:Process"
        }
    }

    $userBackupRoot = [Environment]::GetEnvironmentVariable("BITACORA_BACKUP_ROOT", "User")
    if (-not [string]::IsNullOrWhiteSpace($userBackupRoot)) {
        return @{
            Path = $userBackupRoot
            Source = "Environment:User"
        }
    }

    return @{
        Path = "C:\BitacoraEvidencias\BackupsBitacora"
        Source = "Default"
    }
}

$resolvedBackupRoot = Resolve-BackupRoot -ConfiguredBackupRoot $BackupRoot
$BackupRoot = $resolvedBackupRoot.Path
$backupRootSource = $resolvedBackupRoot.Source

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupName = "BitacoraBackup_$timestamp"
$zipPath = Join-Path $BackupRoot "$backupName.zip"
$logDir = Join-Path $BackupRoot "logs"
$logPath = Join-Path $logDir "Backup_$(Get-Date -Format 'yyyyMMdd').log"

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "$backupName-$([Guid]::NewGuid().ToString('N'))"
$stagingRoot = Join-Path $tempRoot "payload"

function Write-Log {
    param([string]$Message)

    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Message"
    Write-Host $line
    Add-Content -Path $logPath -Value $line
}

function Copy-IfExists {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (-not (Test-Path $SourcePath)) {
        return
    }

    $destinationDir = Split-Path -Parent $DestinationPath
    if (-not [string]::IsNullOrWhiteSpace($destinationDir)) {
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
    }

    Copy-Item -Path $SourcePath -Destination $DestinationPath -Force
}

try {
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

    Write-Log "Inicio de respaldo."
    Write-Log "ProjectRoot: $ProjectRoot"
    Write-Log "BackupRoot: $BackupRoot"
    Write-Log "BackupRootSource: $backupRootSource"
    Write-Log "RetentionDays: $RetentionDays"

    $dataDir =  "C:\BitacoraEvidencias\Data"
    $dbFiles = @("bitacora-evidencias.db")
    if ($IncludeDevDatabase) {
        $dbFiles += "bitacora-evidencias.dev.db"
    }

    $dbBackupDir = Join-Path $stagingRoot "Data"
    New-Item -ItemType Directory -Path $dbBackupDir -Force | Out-Null

    foreach ($dbFile in $dbFiles) {
        $dbPath = Join-Path $dataDir $dbFile
        if (-not (Test-Path $dbPath)) {
            Write-Log "No se encontro BD: $dbPath"
            continue
        }

        Write-Log "Copiando base: $dbPath"
        Copy-IfExists -SourcePath $dbPath -DestinationPath (Join-Path $dbBackupDir $dbFile)
        Copy-IfExists -SourcePath ($dbPath + "-wal") -DestinationPath (Join-Path $dbBackupDir ($dbFile + "-wal"))
        Copy-IfExists -SourcePath ($dbPath + "-shm") -DestinationPath (Join-Path $dbBackupDir ($dbFile + "-shm"))
    }

    $evidenceSource = "C:\BitacoraEvidencias\Evidencias"
    $evidenceDest = Join-Path $stagingRoot "Evidencias"
    if (-not (Test-Path $evidenceSource)) {
        Write-Log "No se encontro carpeta de evidencias: $evidenceSource"
    }
    else {
        Write-Log "Copiando evidencias: $evidenceSource"
        Copy-Item -Path $evidenceSource -Destination $evidenceDest -Recurse -Force
    }

    $manifest = [ordered]@{
        backupName = $backupName
        createdAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        projectRoot = $ProjectRoot
        databaseFiles = $dbFiles
        evidencePath = $evidenceSource
        retentionDays = $RetentionDays
        machineName = $env:COMPUTERNAME
        userName = "$env:USERDOMAIN\$env:USERNAME"
    }

    $manifestPath = Join-Path $stagingRoot "backup-manifest.json"
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8

    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    Write-Log "Creando ZIP: $zipPath"
    Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

    $cutoff = (Get-Date).AddDays(-[Math]::Abs($RetentionDays))
    $removedZips = Get-ChildItem -Path $BackupRoot -File -Filter "*.zip" |
        Where-Object { $_.LastWriteTime -lt $cutoff }
    foreach ($file in $removedZips) {
        Write-Log "Eliminando respaldo por retencion: $($file.FullName)"
        Remove-Item -Path $file.FullName -Force
    }

    $removedLogs = Get-ChildItem -Path $logDir -File -Filter "*.log" |
        Where-Object { $_.LastWriteTime -lt $cutoff }
    foreach ($file in $removedLogs) {
        Remove-Item -Path $file.FullName -Force
    }

    $zipInfo = Get-Item $zipPath
    Write-Log "Respaldo completado. Archivo: $($zipInfo.FullName) ($([Math]::Round($zipInfo.Length / 1MB, 2)) MB)."
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    throw
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
