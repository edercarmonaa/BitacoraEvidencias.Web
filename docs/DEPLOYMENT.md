# Deployment / Despliegue

## English Summary

Deployment is designed for a controlled internal Windows environment. Public documentation uses placeholders and excludes real infrastructure values.

## Deployment Model

The application can be published with .NET and started with PowerShell scripts. Runtime data lives outside the repository:

- Database directory.
- Evidence storage directory.
- Backup directory.
- Certificates.
- Operational logs.

## Required Runtime Inputs

Configure secrets outside Git:

```powershell
BITACORA_DATA_PROTECTION_KEY
BITACORA_HTTPS_CERT_PATH
BITACORA_HTTPS_CERT_PASSWORD
```

Use `<SERVER_IP>` as a placeholder in public documentation.

## Publish

The deployment script creates a published release and can optionally run a backup before replacing the active version.

Example:

```powershell
.\scripts\deploy\Publish-Bitacora.ps1
```

## Manual Start

The application is no longer registered for automatic startup at Windows logon. Start the published app manually with the versioned launcher:

```bat
.\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>
```

The manual launcher:

- Starts the published executable from `C:\BitacoraEvidencias\deploy\current`.
- Reuses the configured certificate and data-protection environment variables.
- Waits for port `5067` and opens `https://<SERVER_IP>:5067/Login`.
- Writes logs under `C:\BitacoraEvidencias\deploy\logs`.

For technical diagnostics, the PowerShell runner can still be used:

```powershell
.\scripts\deploy\Start-BitacoraPublished.ps1 -ServerIp <SERVER_IP>
```

For development-style HTTPS startup from the project source:

```powershell
.\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <SERVER_IP>
```

## Certificate Generation

Certificate generation is handled by script, but generated files and passwords are local secrets.

```powershell
.\scripts\https\New-LanCertificate.ps1 -ServerIp <SERVER_IP> -PersistToUserEnv
```

Do not commit generated certificates.

## Deployment Safety

Before deployment:

- Confirm backup freshness.
- Confirm the data-protection key is available.
- Confirm the certificate path/password are available.
- Confirm storage permissions.
- Confirm the database is not a public sample with real data.
- Confirm no obsolete app startup task exists for `Bitacora Evidencias - Inicio de sesion`.

## Production Hardening Ideas

- Run under a dedicated service account.
- Centralize logs and monitoring.
- Externalize backups to protected storage.
- Add health checks and alerting.
- Consider server-grade database storage if concurrency grows.
