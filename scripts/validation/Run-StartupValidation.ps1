param(
    [string]$ValidationRoot = "",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$projectFile = Join-Path $projectRoot "BitacoraEvidencias.Web.csproj"
$initScript = Join-Path $projectRoot "scripts\db\Initialize-BitacoraDatabase.ps1"
$migrateScript = Join-Path $projectRoot "scripts\storage\Migrate-LegacyStorage.ps1"
$projectStartScript = Join-Path $projectRoot "scripts\https\Start-BitacoraWithHttps.ps1"
$powershellExe = Join-Path $env:WINDIR "System32\WindowsPowerShell\v1.0\powershell.exe"

if ([string]::IsNullOrWhiteSpace($ValidationRoot)) {
    $ValidationRoot = Join-Path $env:TEMP ("bitacora-startup-validation-" + [guid]::NewGuid().ToString("N"))
}

$ValidationRoot = [System.IO.Path]::GetFullPath($ValidationRoot)
New-Item -ItemType Directory -Path $ValidationRoot -Force | Out-Null

$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Check,
        [string]$Status,
        [string]$Detail
    )

    $results.Add([pscustomobject]@{
        Check = $Check
        Status = $Status
        Detail = $Detail
    })
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = $projectRoot
    )

    Push-Location $WorkingDirectory
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $output = & $FilePath @Arguments 2>&1
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = ($output | Out-String).Trim()
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Pop-Location
    }
}

function Invoke-PowerShellFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = $projectRoot
    )

    $psArgs = @(
        "-NoLogo",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $ScriptPath
    ) + $Arguments

    return Invoke-ExternalCommand -FilePath $powershellExe -Arguments $psArgs -WorkingDirectory $WorkingDirectory
}

function Invoke-NormalStartupWithOverrides {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DataDbPath,
        [Parameter(Mandatory = $true)]
        [string]$StorageRoot
    )

    $escapedProject = $projectFile.Replace("'", "''")
    $escapedDb = $DataDbPath.Replace("'", "''")
    $escapedStorage = $StorageRoot.Replace("'", "''")

    $command = @"
`$ErrorActionPreference = 'Stop'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
`$env:ConnectionStrings__DefaultConnection = 'Data Source=$escapedDb'
`$env:Storage__RootPath = '$escapedStorage'
dotnet run --project '$escapedProject' --no-launch-profile
exit `$LASTEXITCODE
"@

    $psArgs = @(
        "-NoLogo",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-Command",
        $command
    )

    return Invoke-ExternalCommand -FilePath $powershellExe -Arguments $psArgs -WorkingDirectory $projectRoot
}

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$missingDbRoot = Join-Path $ValidationRoot "missing-db"
$missingDbStorage = Join-Path $missingDbRoot "storage"
$missingDbPath = Join-Path $missingDbRoot "data\missing.db"

$explicitInitRoot = Join-Path $ValidationRoot "explicit-init"
$explicitInitStorage = Join-Path $explicitInitRoot "storage"
$explicitInitDbPath = Join-Path $explicitInitRoot "data\bitacora.db"

$legacyStorageRoot = Join-Path $ValidationRoot "legacy-storage"
$legacyStoragePath = Join-Path $legacyStorageRoot "Storage"
$migratedStorageRoot = Join-Path $legacyStorageRoot "Evidencias"
$legacyEvidenceRelativePath = "OF-VALIDACION\001\original\sample.txt"
$legacyEvidencePath = Join-Path $legacyStoragePath $legacyEvidenceRelativePath
$migratedEvidencePath = Join-Path $migratedStorageRoot $legacyEvidenceRelativePath

$projectStartRoot = Join-Path $ValidationRoot "project-start"
$projectStartStorage = Join-Path $projectStartRoot "storage"
$projectStartMissingDbPath = Join-Path $projectStartRoot "data\missing.db"

New-Item -ItemType Directory -Path $missingDbStorage -Force | Out-Null
New-Item -ItemType Directory -Path $projectStartStorage -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $legacyEvidencePath -Parent) -Force | Out-Null
[System.IO.File]::WriteAllText($legacyEvidencePath, "legacy-file")

try {
    if (-not $SkipBuild) {
        $buildResult = Invoke-ExternalCommand -FilePath "dotnet" -Arguments @("build", $projectFile)
        Assert-Condition -Condition ($buildResult.ExitCode -eq 0) -Message ("dotnet build fallo. " + $buildResult.Output)
        Add-Result -Check "Build del proyecto" -Status "PASS" -Detail "dotnet build ejecutado correctamente."
    } else {
        Add-Result -Check "Build del proyecto" -Status "SKIP" -Detail "Se omitio con -SkipBuild."
    }

    $missingDbResult = Invoke-NormalStartupWithOverrides -DataDbPath $missingDbPath -StorageRoot $missingDbStorage
    Assert-Condition -Condition ($missingDbResult.ExitCode -ne 0) -Message "El arranque normal no fallo cuando faltaba la base SQLite."
    Assert-Condition -Condition ($missingDbResult.Output -like "*No se encontro la base de datos SQLite*") -Message ("No se encontro el mensaje esperado al arrancar sin DB. " + $missingDbResult.Output)
    Add-Result -Check "Arranque normal falla sin DB preparada" -Status "PASS" -Detail "Se detecto el mensaje esperado para base SQLite faltante."

    $initResult = Invoke-PowerShellFile -ScriptPath $initScript -Arguments @(
        "-DataDbPath", $explicitInitDbPath,
        "-StorageRoot", $explicitInitStorage,
        "-BootstrapAdminPassword", "Validacion123!"
    )
    Assert-Condition -Condition ($initResult.ExitCode -eq 0) -Message ("La inicializacion explicita fallo. " + $initResult.Output)
    Assert-Condition -Condition (Test-Path $explicitInitDbPath) -Message "No se creo la base SQLite durante la inicializacion explicita."
    Assert-Condition -Condition (Test-Path $explicitInitStorage) -Message "No se creo la carpeta de evidencias durante la inicializacion explicita."
    Add-Result -Check "Inicializacion explicita prepara DB y storage" -Status "PASS" -Detail "Initialize-BitacoraDatabase.ps1 creo los artefactos esperados."

    $migrateResult = Invoke-PowerShellFile -ScriptPath $migrateScript -Arguments @(
        "-LegacyStoragePath", $legacyStoragePath,
        "-TargetStorageRoot", $migratedStorageRoot
    )
    Assert-Condition -Condition ($migrateResult.ExitCode -eq 0) -Message ("La migracion de storage legado fallo. " + $migrateResult.Output)
    Assert-Condition -Condition (-not (Test-Path $legacyStoragePath)) -Message "La carpeta legado sigue existiendo despues de migrar."
    Assert-Condition -Condition (Test-Path $migratedEvidencePath) -Message "El archivo legado no llego al destino esperado."
    Assert-Condition -Condition ([System.IO.File]::ReadAllText($migratedEvidencePath) -eq "legacy-file") -Message "El contenido del archivo migrado no coincide."
    Add-Result -Check "Migracion explicita de storage legado" -Status "PASS" -Detail "Migrate-LegacyStorage.ps1 movio el archivo de prueba correctamente."

    $projectStartResult = Invoke-PowerShellFile -ScriptPath $projectStartScript -Arguments @(
        "-ServerIp", "127.0.0.1",
        "-DataDbPath", $projectStartMissingDbPath,
        "-StorageRoot", $projectStartStorage
    )
    Assert-Condition -Condition ($projectStartResult.ExitCode -ne 0) -Message "Start-BitacoraWithHttps.ps1 no bloqueo el arranque cuando faltaba la DB."
    Assert-Condition -Condition ($projectStartResult.Output -like "*Initialize-BitacoraDatabase.ps1*") -Message ("El script HTTPS no sugirio la inicializacion explicita esperada. " + $projectStartResult.Output)
    Add-Result -Check "Prevalidacion de Start-BitacoraWithHttps" -Status "PASS" -Detail "El script detecto DB faltante y sugirio Initialize-BitacoraDatabase.ps1."
}
catch {
    Add-Result -Check "Ejecucion general" -Status "FAIL" -Detail $_.Exception.Message
}

$results | Format-Table -AutoSize

$failed = @($results | Where-Object { $_.Status -eq "FAIL" })
if ($failed.Count -gt 0) {
    exit 1
}

exit 0
