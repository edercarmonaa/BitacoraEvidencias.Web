param(
    [Parameter(Mandatory = $true)]
    [string]$ServerIp,

    [string]$OutputDir = "C:\BitacoraEvidencias\certs",
    [ValidateRange(1,10)][int]$YearsValid = 3,
    [switch]$PersistToUserEnv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not ([System.Net.IPAddress]::TryParse($ServerIp, [ref]([System.Net.IPAddress]::None)))) {
    throw "ServerIp no tiene formato de IP valido: $ServerIp"
}

$pfxPath = Join-Path $OutputDir "bitacora-lan.pfx"
$cerPath = Join-Path $OutputDir "bitacora-lan.cer"
$credentialPath = Join-Path $OutputDir 'bitacora-lan.credential.xml'
if ((Test-Path -LiteralPath $pfxPath) -or (Test-Path -LiteralPath $cerPath) -or (Test-Path -LiteralPath $credentialPath)) { throw 'Ya existe material HTTPS en el destino. No se sobrescribe; usar una carpeta nueva para una renovacion revisada.' }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$randomBytes = New-Object byte[] 32
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $random.GetBytes($randomBytes) } finally { $random.Dispose() }
$plainPassword = [Convert]::ToBase64String($randomBytes)
$securePassword = ConvertTo-SecureString -String $plainPassword -AsPlainText -Force

$cert = New-SelfSignedCertificate `
    -Type SSLServerAuthentication `
    -Subject "CN=$ServerIp" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears($YearsValid) `
    -FriendlyName "BitacoraEvidencias LAN HTTPS" `
    -TextExtension @("2.5.29.17={text}IPAddress=$ServerIp")

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
[Management.Automation.PSCredential]::new('HTTPS',$securePassword) | Export-Clixml -LiteralPath $credentialPath -NoClobber
$env:BITACORA_HTTPS_CERT_PATH = $pfxPath
$env:BITACORA_HTTPS_CERT_PASSWORD = $plainPassword

if ($PersistToUserEnv) {
    [Environment]::SetEnvironmentVariable("BITACORA_HTTPS_CERT_PATH", $pfxPath, "User")
    [Environment]::SetEnvironmentVariable("BITACORA_HTTPS_CERT_PASSWORD", $plainPassword, "User")
}

Write-Host ""
Write-Host "Certificado generado:" -ForegroundColor Green
Write-Host "  PFX: $pfxPath"
Write-Host "  CER: $cerPath"
Write-Host ""
Write-Host "Variables HTTPS configuradas solo en esta sesion; no se imprime la contrasena. Custodiarla por el mecanismo externo autorizado."
Write-Host "Credencial HTTPS protegida con DPAPI: $credentialPath (misma cuenta/equipo)."
Write-Host ""
Write-Host "Ejecuta luego:"
Write-Host "  .\scripts\deploy\Start-BitacoraSqlServer.ps1 -ServerAddress $ServerIp -AppFolder App-backup-final"
Write-Host ""
Write-Host "Para que clientes confien en HTTPS, instala el .cer en 'Trusted Root Certification Authorities' en cada equipo cliente."
