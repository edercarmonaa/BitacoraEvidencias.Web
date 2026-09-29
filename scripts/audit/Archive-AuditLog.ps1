param(
    [string]$ArchiveRoot = "C:\BitacoraEvidencias\AuditArchive",
    [string]$DatabasePath = "C:\BitacoraEvidencias\Data\bitacora-evidencias.db",
    [int]$RetentionDays = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)

    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Message"
    Write-Host $line
    Add-Content -LiteralPath $script:LogPath -Value $line
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

    $state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $script:RunStatePath -Encoding UTF8
}

function Write-ArchiveStatus {
    param(
        [string]$Status,
        [string]$Details,
        [object[]]$ToolOutput = @()
    )

    $statusBody = [ordered]@{
        status = $Status
        details = $Details
        ranAtLocal = $script:StartedAtLocal
        updatedAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        machineName = $env:COMPUTERNAME
        userName = "$env:USERDOMAIN\$env:USERNAME"
        archiveRoot = $ArchiveRoot
        databasePath = $DatabasePath
        retentionDays = $RetentionDays
        logPath = $script:LogPath
        toolOutput = $ToolOutput
    }

    $statusBody | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $script:StatusPath -Encoding UTF8
}

if ($RetentionDays -le 0) {
    throw "RetentionDays debe ser un entero positivo."
}

$logsRoot = Join-Path $ArchiveRoot "logs"
$script:LogPath = Join-Path $logsRoot "ArchiveAudit_$(Get-Date -Format 'yyyyMMdd').log"
$script:RunStatePath = Join-Path $ArchiveRoot "latest-run.json"
$script:StatusPath = Join-Path $ArchiveRoot "latest-status.json"
$script:StartedAtLocal = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

New-Item -ItemType Directory -Path $ArchiveRoot -Force | Out-Null
New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null

Write-Log "Inicio de archivado de AuditLog."
Write-Log "ArchiveRoot: $ArchiveRoot"
Write-Log "DatabasePath: $DatabasePath"
Write-Log "RetentionDays: $RetentionDays"
Write-Log "ProcessId: $PID"
Write-RunState -Status "RUNNING" -Step "Inicio de archivado"

try {
    if (-not (Test-Path -LiteralPath $DatabasePath)) {
        throw "No existe la base SQLite en: $DatabasePath"
    }

    $toolDll = "C:\BitacoraEvidencias\tools\audit\BitacoraMonitorQuery.dll"
    if (-not (Test-Path -LiteralPath $toolDll)) {
        throw "No existe la herramienta de archivado: $toolDll"
    }

    Write-RunState -Status "RUNNING" -Step "Ejecutando herramienta de archivado"
    Write-Log "STEP-START | ArchiveTool"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $output = & dotnet $toolDll archive $DatabasePath $ArchiveRoot $RetentionDays 2>&1
    $exitCode = $LASTEXITCODE
    $watch.Stop()
    Write-Log "STEP-END | ArchiveTool | DurationSeconds=$([Math]::Round($watch.Elapsed.TotalSeconds, 2)) | ExitCode=$exitCode"

    if ($exitCode -ne 0) {
        foreach ($line in $output) {
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                Write-Log "TOOL-ERROR | $line"
            }
        }

        throw "La herramienta de archivado devolvio codigo $exitCode."
    }

    foreach ($line in $output) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            Write-Log "TOOL | $line"
        }
    }

    Write-ArchiveStatus -Status "COMPLETED" -Details "Archivado de AuditLog completado." -ToolOutput $output
    Write-RunState -Status "COMPLETED" -Step "Archivado completado"
    Write-Log "Archivado de AuditLog completado."
}
catch {
    $errorMessage = $_.Exception.Message
    Write-Log "ERROR: $errorMessage"
    Write-ArchiveStatus -Status "FAILED" -Details $errorMessage
    Write-RunState -Status "FAILED" -Step "Error general" -Details $errorMessage
    throw
}
