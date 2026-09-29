param(
    [Parameter(Mandatory = $true)]
    [string]$ServerIp,

    [string]$OutputDir = "C:\BitacoraEvidencias\certs",
    [int]$YearsValid = 3,
    [switch]$PersistToUserEnv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not ([System.Net.IPAddress]::TryParse($ServerIp, [ref]([System.Net.IPAddress]::None)))) {
    throw "ServerIp no tiene formato de IP valido: $ServerIp"
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$pfxPath = Join-Path $OutputDir "bitacora-lan.pfx"
$cerPath = Join-Path $OutputDir "bitacora-lan.cer"

$charset = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray()
$passwordChars = 1..24 | ForEach-Object { $charset | Get-Random }
$plainPassword = -join $passwordChars
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

if ($PersistToUserEnv) {
    [Environment]::SetEnvironmentVariable("BITACORA_HTTPS_CERT_PATH", $pfxPath, "User")
    [Environment]::SetEnvironmentVariable("BITACORA_HTTPS_CERT_PASSWORD", $plainPassword, "User")
}

Write-Host ""
Write-Host "Certificado generado:" -ForegroundColor Green
Write-Host "  PFX: $pfxPath"
Write-Host "  CER: $cerPath"
Write-Host ""
Write-Host "Variables de entorno para esta sesion:"
Write-Host "  `$env:BITACORA_HTTPS_CERT_PATH = '$pfxPath'"
Write-Host "  `$env:BITACORA_HTTPS_CERT_PASSWORD = '$plainPassword'"
Write-Host ""
Write-Host "Ejecuta luego:"
Write-Host "  .\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp $ServerIp"
Write-Host ""
Write-Host "Para que clientes confien en HTTPS, instala el .cer en 'Trusted Root Certification Authorities' en cada equipo cliente."
