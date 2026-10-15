param(
    [ValidateSet('preflight','import','verify','upgrade')][string]$Mode = 'preflight',
    [string]$TrialRoot = 'C:\BitacoraEvidencias_Pruebas',
    [string]$SourceDatabase = '',
    [string]$SourceEvidence = '',
    [switch]$Production,
    [switch]$TrustLocalSqlCertificate
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$tool = Join-Path $projectRoot 'tools\BitacoraMigration\bin\Release\net9.0\BitacoraMigration.dll'
if (-not (Test-Path -LiteralPath $tool)) { throw 'Compila primero tools/BitacoraMigration/BitacoraMigration.csproj en Release.' }
if (-not $SourceDatabase) { $SourceDatabase = Join-Path $TrialRoot 'Input\Data\bitacora-evidencias.db' }
if (-not $SourceEvidence) { $SourceEvidence = Join-Path $TrialRoot 'Input\Evidencias' }
if ($Production -and $Mode -in @('import','upgrade')) {
    if ((Read-Host 'Confirma ventana de mantenimiento, respaldo final y servicio SQLite detenido: escribe MIGRAR PRODUCCION') -cne 'MIGRAR PRODUCCION') {
        throw 'Migracion productiva cancelada.'
    }
}
$previousConnection = [Environment]::GetEnvironmentVariable('BITACORA_MIGRATION_SQL_CONNECTION','Process')
try {
    if ($Mode -ne 'preflight') {
        [string]$user = Read-Host 'Usuario SQL (no se guardara)'
        $password = Read-Host 'Contrasena SQL' -AsSecureString
        $credential = [Management.Automation.PSCredential]::new($user, $password)
        $connection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
        $connection.set_DataSource('.\Auditoria')
        $connection.set_InitialCatalog($(if ($Production) { 'BitacoraEvidencias' } else { 'BitacoraEvidencias_Pruebas' }))
        $connection.set_UserID([string]$user)
        $connection.set_Password($credential.GetNetworkCredential().Password)
        $connection.set_Encrypt($true)
        $connection.set_TrustServerCertificate([bool]$TrustLocalSqlCertificate)
        $connection.set_PersistSecurityInfo($false)
        $env:BITACORA_MIGRATION_SQL_CONNECTION = $connection.ConnectionString
    }
    $arguments = @($tool, $Mode, $SourceDatabase, $SourceEvidence)
    if ($Production) { $arguments += '--production' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Proceso detenido con codigo $LASTEXITCODE. No iniciar la aplicacion de destino." }
} finally {
    [Environment]::SetEnvironmentVariable('BITACORA_MIGRATION_SQL_CONNECTION',$previousConnection,'Process')
    Remove-Variable credential,password,connection -ErrorAction SilentlyContinue
}
