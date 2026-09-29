# Privacy and Data Handling / Privacidad y Manejo de Datos

## English Summary

This public repository is an anonymized portfolio case study. It must not contain real operational data.

## Data Categories

The application model can handle:

- Administrative records.
- Case metadata.
- Evidence metadata.
- Evidence image files.
- User accounts and roles.
- Audit events.
- Import previews and operational reports.

Some of these categories can be sensitive in a real institutional environment.

## Public Repository Policy

Never publish:

- Real names, identifiers, case details, or record numbers.
- Evidence photos or thumbnails.
- User databases or password hashes.
- Audit logs from a real environment.
- Internal IP addresses, hostnames, certificate thumbprints, or deployment paths.
- Backup archives or exported audit logs.

## Runtime Storage

Runtime data belongs outside the repository:

- SQLite database files.
- Evidence folders.
- Backups.
- Monitoring output.
- Audit archive output.
- Certificates and keys.

## Retention

Audit data can be retained in the active database for a configured period and then archived. Archive files are still sensitive operational records and must be protected.

## Anonymization

For portfolio use:

- Replace internal IPs with `<SERVER_IP>` or `127.0.0.1`.
- Replace real users with synthetic users.
- Replace real evidence with generated or synthetic files.
- Remove dates, hostnames, and operational details that could identify the original environment.
