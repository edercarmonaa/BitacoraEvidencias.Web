param(
    [string]$DeploymentManifest = 'C:\BitacoraEvidencias\deploy-sql\active.json',
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$deployment = Get-Content -LiteralPath $DeploymentManifest -Raw | ConvertFrom-Json
if ($deployment.Provider -ne 'SqlServer' -or $deployment.Version -ne '0.2.0') {
    throw 'El manifiesto no corresponde a la instalacion SQL 0.2.0.'
}
$protected = Import-Clixml -LiteralPath $deployment.ConfigurationFile
if ($protected -isnot [Security.SecureString]) { throw 'Configuracion protegida no valida para esta cuenta Windows.' }
$previous = @{}
$pointer = [IntPtr]::Zero
try {
    # Persist the feature selection in the active manifest so a manual start uses
    # the same setting as the deployment and the backup/restart cycle.
    $summariesEnabled = $false
    if ($deployment.PSObject.Properties.Name -contains 'ReportSummariesEnabled') {
        if ($deployment.ReportSummariesEnabled -isnot [bool]) { throw 'ReportSummariesEnabled debe ser booleano.' }
        $summariesEnabled = $deployment.ReportSummariesEnabled
    }
    $previous['Reports__SummariesEnabled'] = [Environment]::GetEnvironmentVariable('Reports__SummariesEnabled','Process')
    [Environment]::SetEnvironmentVariable('Reports__SummariesEnabled',$summariesEnabled.ToString().ToLowerInvariant(),'Process')
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($protected)
    $settings = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) | ConvertFrom-Json
    foreach ($name in @('BITACORA_DATA_PROTECTION_KEY','BITACORA_HTTPS_CERT_PATH','BITACORA_HTTPS_CERT_PASSWORD')) {
        $value = $settings.$name
        if ([string]::IsNullOrWhiteSpace($value)) { throw ('Falta la configuracion protegida '+$name) }
        $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process')
        [Environment]::SetEnvironmentVariable($name,$value,'Process')
    }
    & (Join-Path $PSScriptRoot 'Start-BitacoraSqlProduction.ps1') `
        -ApplicationFolder $deployment.ApplicationFolder -EvidenceRoot $deployment.EvidenceRoot `
        -ServerAddress $deployment.ServerAddress -HttpsPort $deployment.HttpsPort `
        -SqlCredentialFile $deployment.SqlCredentialFile `
        -TrustLocalSqlCertificate:([bool]$deployment.TrustLocalSqlCertificate) -ValidateOnly:$ValidateOnly
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $protected.Dispose()
    Remove-Variable settings,value -ErrorAction SilentlyContinue
}
