# Local Setup / Puesta en Marcha Local

## English Summary

These instructions describe a local, anonymized development setup. They do not use production data, real evidence files, certificates, or internal network addresses.

## Requisitos

- Windows or another OS capable of running .NET 9.
- .NET SDK 9.
- PowerShell for operational scripts.
- A local working copy of the repository.

## Configuration

The public `appsettings.json` uses local-safe defaults:

- SQLite database path under `Data/`.
- Evidence storage under local `Storage`.
- Sensitive-data protection key read from an environment variable.

For local-only overrides, create a file ignored by Git, for example:

```text
appsettings.Local.json
```

Do not commit local overrides.

## Database

The repository must not include real `.db` files. Create or prepare a local database through the existing startup and migration workflow used by the application.

The application connection string is configured under:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=Data/bitacora-evidencias.db"
}
```

## Sensitive Key

If testing protected sensitive data, configure:

```powershell
$env:BITACORA_DATA_PROTECTION_KEY = "<local-development-key>"
```

Use a local test key only. Never commit it.

## Run Locally

Typical local execution:

```powershell
dotnet run
```

The public launch profile uses:

```text
https://127.0.0.1:5067
```

## Development Notes

- Keep generated databases, logs, backups, and evidence files out of Git.
- Use synthetic test data.
- Do not use real institutional records in a public clone.
