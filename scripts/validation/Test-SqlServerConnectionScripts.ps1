# Executes the real wrappers with mocked prompts and child processes. No SQL connection,
# application startup, persistent environment change or filesystem mutation occurs.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$global:BitacoraConnectionTestState = @{}
$global:BitacoraConnectionTestState.FakePassword = 'fake;quote="'';equals=value'
$global:BitacoraConnectionTestState.ExpectedDatabase = ''
$global:BitacoraConnectionTestState.ExpectedTrust = $false
$global:BitacoraConnectionTestState.Calls = 0

function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if ($AsSecureString) { return ConvertTo-SecureString $global:BitacoraConnectionTestState.FakePassword -AsPlainText -Force }
    return [Management.Automation.PSObject]::new('regression_user')
}
function Test-Path { param($LiteralPath) return $true }
function Get-NetTCPConnection { param($LocalPort, $State, $ErrorAction) }
function Push-Location { param($Path) }
function Pop-Location { }
function dotnet {
    param([Parameter(ValueFromRemainingArguments = $true)][object[]]$CommandArguments)
    $value = if ($CommandArguments[0] -like '*BitacoraMigration.dll') {
        $env:BITACORA_MIGRATION_SQL_CONNECTION
    } else { $env:ConnectionStrings__DefaultConnection }
    $parsed = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($value)
    if ($parsed.DataSource -cne '.\Auditoria' -or
        $parsed.InitialCatalog -cne $global:BitacoraConnectionTestState.ExpectedDatabase -or
        $parsed.UserID -cne 'regression_user' -or
        $parsed.Password -cne $global:BitacoraConnectionTestState.FakePassword -or
        -not $parsed.Encrypt -or
        $parsed.TrustServerCertificate -ne $global:BitacoraConnectionTestState.ExpectedTrust -or
        $parsed.PersistSecurityInfo) { throw 'La captura y serializacion de credenciales no conserva los valores esperados.' }
    $global:BitacoraConnectionTestState.Calls++
    $global:LASTEXITCODE = 0
}

$saved = @{}
foreach ($name in @('BITACORA_DATA_PROTECTION_KEY','BITACORA_HTTPS_CERT_PATH','BITACORA_HTTPS_CERT_PASSWORD')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
    [Environment]::SetEnvironmentVariable($name,'fake-regression-value','Process')
}
$initialMigration = [Environment]::GetEnvironmentVariable('BITACORA_MIGRATION_SQL_CONNECTION','Process')
$initialApp = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection','Process')
try {
    foreach ($trust in @($false,$true)) {
        $global:BitacoraConnectionTestState.ExpectedTrust = $trust
        foreach ($production in @($false,$true)) {
            $global:BitacoraConnectionTestState.ExpectedDatabase = if ($production) { 'BitacoraEvidencias' } else { 'BitacoraEvidencias_Pruebas' }
            & (Join-Path $root 'scripts\migration\Invoke-BitacoraSqlMigration.ps1') -Mode verify -Production:$production -TrustLocalSqlCertificate:$trust
        }
        $global:BitacoraConnectionTestState.ExpectedDatabase = 'BitacoraEvidencias_Pruebas'
        & (Join-Path $root 'scripts\deploy\Start-BitacoraSqlServer.ps1') -TrustLocalSqlCertificate:$trust
    }
    if ($global:BitacoraConnectionTestState.Calls -ne 6) { throw 'No se ejercitaron todos los casos.' }
    if ([Environment]::GetEnvironmentVariable('BITACORA_MIGRATION_SQL_CONNECTION','Process') -cne $initialMigration -or
        [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection','Process') -cne $initialApp) {
        throw 'Los wrappers no restauraron las variables del proceso.'
    }
    Write-Host 'OK: seis ejecuciones de los scripts reales, entradas PSObject, caracteres especiales, destinos, TLS y restauracion de variables.'
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
    [Environment]::SetEnvironmentVariable('BITACORA_MIGRATION_SQL_CONNECTION',$initialMigration,'Process')
    [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection',$initialApp,'Process')
}
