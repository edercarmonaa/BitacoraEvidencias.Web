param(
    [string]$MonitorRoot = "C:\BitacoraEvidencias\Monitoreo",
    [string]$AppUrl = "https://127.0.0.1:5067/Login",
    [string]$DataRoot = "C:\BitacoraEvidencias\Data",
    [string]$EvidenceRoot = "C:\BitacoraEvidencias\Evidencias",
    [string]$DatabasePath = "C:\BitacoraEvidencias\Data\bitacora-evidencias.db",
    [double]$MinFreeDiskGb = 25,
    [double]$MaxEvidenceGb = 50,
    [double]$MaxDatabaseGb = 5,
    [int]$BackupMaxAgeHours = 24,
    [int]$CriticalEventHours = 24,
    [int]$AppRequestTimeoutSec = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-BackupRoot {
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

    $machineBackupRoot = [Environment]::GetEnvironmentVariable("BITACORA_BACKUP_ROOT", "Machine")
    if (-not [string]::IsNullOrWhiteSpace($machineBackupRoot)) {
        return @{
            Path = $machineBackupRoot
            Source = "Environment:Machine"
        }
    }

    return @{
        Path = "C:\BitacoraEvidencias\BackupsBitacora"
        Source = "Default"
    }
}

function Write-Log {
    param([string]$Message)

    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Message"
    Write-Host $line
    Add-Content -Path $script:LogPath -Value $line
}

function Write-RunState {
    param(
        [string]$Status,
        [string]$Step,
        [string]$Details = ""
    )

    $state = [ordered]@{
        status = $Status
        step = $Step
        details = $Details
        updatedAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        machineName = $env:COMPUTERNAME
        userName = "$env:USERDOMAIN\$env:USERNAME"
        processId = $PID
        logPath = $script:LogPath
        statusPath = $script:StatusPath
    }

    $state | ConvertTo-Json -Depth 4 | Set-Content -Path $script:RunStatePath -Encoding UTF8
}

function Invoke-MonitorStep {
    param(
        [string]$Name,
        [scriptblock]$ScriptBlock
    )

    Write-RunState -Status "RUNNING" -Step $Name
    Write-Log "STEP-START | $Name"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        & $ScriptBlock
    }
    finally {
        $watch.Stop()
        Write-Log "STEP-END | $Name | DurationSeconds=$([Math]::Round($watch.Elapsed.TotalSeconds, 2))"
        Write-RunState -Status "RUNNING" -Step "Completed: $Name" -Details "DurationSeconds=$([Math]::Round($watch.Elapsed.TotalSeconds, 2))"
    }
}

function Get-DirectorySizeBytes {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0L
    }

    $sum = (Get-ChildItem -LiteralPath $Path -Recurse -File -Force -ErrorAction Stop |
        Measure-Object -Property Length -Sum).Sum

    if ($null -eq $sum) {
        return 0L
    }

    return [int64]$sum
}

function Convert-ToGb {
    param([double]$Bytes)

    return [Math]::Round($Bytes / 1GB, 2)
}

function Add-CheckResult {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Details
    )

    $script:CheckResults.Add([ordered]@{
        name = $Name
        status = $Status
        details = $Details
    }) | Out-Null

    Write-Log "$Name | $Status | $Details"
}

function Invoke-MonitorQuery {
    param(
        [string]$DatabasePath,
        [int]$HoursBack
    )

    $toolDll = "C:\BitacoraEvidencias\tools\monitor\BitacoraMonitorQuery.dll"
    if (-not (Test-Path -LiteralPath $toolDll)) {
        throw "No existe la herramienta de consulta: $toolDll"
    }

    $output = & dotnet $toolDll $DatabasePath $HoursBack 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "La consulta de eventos criticos fallo: $($output -join ' ')"
    }

    $result = [ordered]@{}
    foreach ($line in $output) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line -notmatch "=") {
            continue
        }

        $parts = $line.Split("=", 2)
        $name = $parts[0].Trim()
        $value = 0
        [void][int64]::TryParse($parts[1].Trim(), [ref]$value)
        $result[$name] = $value
    }

    return $result
}

$backupRootInfo = Resolve-BackupRoot
$backupRoot = $backupRootInfo.Path
$backupRootSource = $backupRootInfo.Source

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logDir = Join-Path $MonitorRoot "logs"
$statusDir = Join-Path $MonitorRoot "status"
$statusPath = Join-Path $statusDir "MonitorStatus_$timestamp.json"
$latestStatusPath = Join-Path $MonitorRoot "latest-status.json"
$runStatePath = Join-Path $MonitorRoot "latest-run.json"
$script:LogPath = Join-Path $logDir "Monitor_$(Get-Date -Format 'yyyyMMdd').log"
$script:StatusPath = $statusPath
$script:RunStatePath = $runStatePath
$script:CheckResults = [System.Collections.Generic.List[object]]::new()

$summary = [ordered]@{
    ranAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    machineName = $env:COMPUTERNAME
    userName = "$env:USERDOMAIN\$env:USERNAME"
    appUrl = $AppUrl
    backupRoot = $backupRoot
    backupRootSource = $backupRootSource
    checks = @()
    criticalEvents = [ordered]@{}
}

try {
    New-Item -ItemType Directory -Path $MonitorRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    New-Item -ItemType Directory -Path $statusDir -Force | Out-Null

    Write-Log "Inicio de monitoreo."
    Write-Log "AppUrl: $AppUrl"
    Write-Log "BackupRoot: $backupRoot"
    Write-Log "BackupRootSource: $backupRootSource"
    Write-Log "ProcessId: $PID"
    Write-RunState -Status "RUNNING" -Step "Inicio de monitoreo"

    Invoke-MonitorStep -Name "AppAvailability" -ScriptBlock {
        try {
            $response = Invoke-WebRequest -Uri $AppUrl -Method Get -TimeoutSec $AppRequestTimeoutSec -UseBasicParsing
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Add-CheckResult -Name "AppAvailability" -Status "OK" -Details "HTTP $($response.StatusCode)"
            }
            else {
                Add-CheckResult -Name "AppAvailability" -Status "ALERT" -Details "HTTP $($response.StatusCode)"
            }
        }
        catch {
            Add-CheckResult -Name "AppAvailability" -Status "ALERT" -Details $_.Exception.Message
        }
    }

    Invoke-MonitorStep -Name "RecentBackup" -ScriptBlock {
        try {
            $cutoff = (Get-Date).AddHours(-[Math]::Abs($BackupMaxAgeHours))
            $recentZip = Get-ChildItem -LiteralPath $backupRoot -File -Filter "*.zip" -ErrorAction Stop |
                Where-Object { $_.LastWriteTime -ge $cutoff } |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1

            if ($null -ne $recentZip) {
                Add-CheckResult -Name "RecentBackup" -Status "OK" -Details "$($recentZip.Name) | $($recentZip.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
            }
            else {
                Add-CheckResult -Name "RecentBackup" -Status "ALERT" -Details "No se encontro ZIP en las ultimas $BackupMaxAgeHours horas."
            }
        }
        catch {
            Add-CheckResult -Name "RecentBackup" -Status "ALERT" -Details $_.Exception.Message
        }
    }

    Invoke-MonitorStep -Name "DiskFreeC" -ScriptBlock {
        try {
            $drive = Get-PSDrive -Name C -ErrorAction Stop
            $freeGb = Convert-ToGb -Bytes $drive.Free
            if ($freeGb -ge $MinFreeDiskGb) {
                Add-CheckResult -Name "DiskFreeC" -Status "OK" -Details "$freeGb GB libres."
            }
            else {
                Add-CheckResult -Name "DiskFreeC" -Status "ALERT" -Details "$freeGb GB libres. Umbral: $MinFreeDiskGb GB."
            }
        }
        catch {
            Add-CheckResult -Name "DiskFreeC" -Status "ALERT" -Details $_.Exception.Message
        }
    }

    Invoke-MonitorStep -Name "EvidenceSize" -ScriptBlock {
        try {
            $evidenceBytes = Get-DirectorySizeBytes -Path $EvidenceRoot
            $evidenceGb = Convert-ToGb -Bytes $evidenceBytes
            if ($evidenceGb -le $MaxEvidenceGb) {
                Add-CheckResult -Name "EvidenceSize" -Status "OK" -Details "$evidenceGb GB. Umbral: $MaxEvidenceGb GB."
            }
            else {
                Add-CheckResult -Name "EvidenceSize" -Status "ALERT" -Details "$evidenceGb GB. Umbral: $MaxEvidenceGb GB."
            }
        }
        catch {
            Add-CheckResult -Name "EvidenceSize" -Status "ALERT" -Details $_.Exception.Message
        }
    }

    Invoke-MonitorStep -Name "DatabaseSize" -ScriptBlock {
        try {
            if (-not (Test-Path -LiteralPath $DatabasePath)) {
                throw "No existe la base: $DatabasePath"
            }

            $dbFile = Get-Item -LiteralPath $DatabasePath -ErrorAction Stop
            $dbGb = Convert-ToGb -Bytes $dbFile.Length
            if ($dbGb -le $MaxDatabaseGb) {
                Add-CheckResult -Name "DatabaseSize" -Status "OK" -Details "$dbGb GB. Umbral: $MaxDatabaseGb GB."
            }
            else {
                Add-CheckResult -Name "DatabaseSize" -Status "ALERT" -Details "$dbGb GB. Umbral: $MaxDatabaseGb GB."
            }
        }
        catch {
            Add-CheckResult -Name "DatabaseSize" -Status "ALERT" -Details $_.Exception.Message
        }
    }

    Invoke-MonitorStep -Name "CriticalEventsQuery" -ScriptBlock {
        try {
            $criticalEventCounts = Invoke-MonitorQuery -DatabasePath $DatabasePath -HoursBack $CriticalEventHours
            $summary.criticalEvents = $criticalEventCounts
            foreach ($name in $criticalEventCounts.Keys) {
                Write-Log "CriticalEvents | $name=$($criticalEventCounts[$name])"
            }
            Add-CheckResult -Name "CriticalEventsQuery" -Status "OK" -Details "Consulta completada para ultimas $CriticalEventHours horas."
        }
        catch {
            Add-CheckResult -Name "CriticalEventsQuery" -Status "ALERT" -Details $_.Exception.Message
        }
    }

    $summary.checks = $script:CheckResults
    $overallStatus = if ($script:CheckResults.Where({ $_.status -eq "ALERT" }).Count -gt 0) { "ALERT" } else { "OK" }
    $summary.overallStatus = $overallStatus
    $summary.completedAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $summary | ConvertTo-Json -Depth 6 | Set-Content -Path $statusPath -Encoding UTF8
    $summary | ConvertTo-Json -Depth 6 | Set-Content -Path $latestStatusPath -Encoding UTF8
    Write-RunState -Status "COMPLETED" -Step "Monitoreo completado" -Details "OverallStatus=$overallStatus"

    Write-Log "Monitoreo completado. OverallStatus=$overallStatus"
}
catch {
    $errorMessage = $_.Exception.Message
    Write-Log "ERROR: $errorMessage"
    Write-RunState -Status "FAILED" -Step "Error general" -Details $errorMessage
    throw
}

