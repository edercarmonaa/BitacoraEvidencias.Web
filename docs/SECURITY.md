# Security / Seguridad

## English Summary

This project treats security as part of application design and daily operation, not only as code. The public repository is anonymized and must not contain real data, credentials, certificates, private keys, internal addresses, or operational evidence.

## Resumen en Espanol

La seguridad se aborda desde el diseno de la aplicacion y desde la operacion diaria. El repositorio publico esta anonimizado y no debe contener datos reales, credenciales, certificados, llaves privadas, direcciones internas ni evidencias operativas.

## Authentication and Authorization

- Cookie-based authentication is used for the web application.
- Role-based authorization restricts administrative areas.
- User administration includes temporary password workflows.
- Session security is refreshed so role changes can affect active users.

## Password Handling

Passwords are not stored in plain text. The application uses password hashing and salts through the security layer. Temporary passwords are operationally sensitive and must not be logged or committed.

## Audit Logging

The application records important events such as login outcomes, user administration, data changes, evidence actions, and security-relevant operations.

Audit data supports traceability, incident review, and operational accountability. Old audit entries can be archived to log files by operational scripts.

## Sensitive Data Protection

Sensitive values can be protected using key material provided outside the repository. The expected key source is an environment variable:

```powershell
BITACORA_DATA_PROTECTION_KEY
```

The key must be generated and stored outside Git. Losing the key can make protected data unreadable.

## Certificates and HTTPS

HTTPS startup scripts read certificate configuration from environment variables:

```powershell
BITACORA_HTTPS_CERT_PATH
BITACORA_HTTPS_CERT_PASSWORD
```

Certificates, private keys, and generated `.pfx` files must never be committed.

## Repository Hygiene

Do not commit:

- `Data/*.db`, `*.db-wal`, `*.db-shm`
- Evidence photos or thumbnails.
- Backups, ZIPs, exports, imports, logs, or smoke-test result files.
- `appsettings.Production.json`, `appsettings.Local.json`, or any file containing secrets.
- Certificates or private keys.
- Internal reports with real IPs, operational findings, or identifiable data.

## Public Documentation Rules

- Use `127.0.0.1` for local examples.
- Use `<SERVER_IP>` for deployment placeholders.
- Do not include real usernames, hostnames, internal IPs, certificate thumbprints, or operational dates tied to private environments.

## Residual Risks

- SQLite is appropriate for the target scenario but has concurrency limits.
- Filesystem-backed evidence requires strong OS-level permissions and backup discipline.
- Local script-based monitoring is useful but less mature than centralized alerting.
- A public case-study repository must be continuously reviewed to avoid accidental disclosure.
