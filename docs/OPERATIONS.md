# Operations / Operacion Diaria

## English Summary

The project includes operational scripts because the original scenario required a small internal system that could be backed up, monitored, started, and reviewed without a large platform team.

## Daily Review

Recommended checks:

- Application is reachable.
- Recent backup exists.
- Disk space is healthy.
- Evidence storage size is within threshold.
- Database size is within threshold.
- Audit log does not show unattended critical events.

## Manual Application Start

The app is intentionally not registered as a Windows logon scheduled task. Start it manually when needed:

```bat
.\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>
```

Operational convenience launchers may exist outside the repository:

- `C:\BitacoraEvidencias\Iniciar Bitacora Manual.cmd`
- `Escritorio del usuario operativo\Iniciar Bitacora Manual.cmd`

The launcher writes startup logs under `C:\BitacoraEvidencias\deploy\logs`.

## Backup

Backup scripts are responsible for collecting database and evidence storage into operational backup locations. Backups must be stored outside the repository.

```powershell
.\scripts\backup\Backup-Bitacora.ps1
```

## Monitoring

Monitoring is script-based and writes local diagnostic files:

- `latest-run.json`
- `latest-status.json`
- dated log files
- status history files

Run manually:

```powershell
.\scripts\monitor\Monitor-Bitacora.ps1 -AppUrl https://127.0.0.1:5067/Login
```

In a real internal deployment, the URL should be supplied with `<SERVER_IP>`.

## Audit Log Archiving

Old audit records can be exported to log files and removed from the active database according to retention settings.

```powershell
.\scripts\audit\Archive-AuditLog.ps1
```

The archive output is operational data and must not be committed.

## Smoke Tests

Smoke tests validate key workflows after deployment or significant changes. They require a test admin password supplied outside Git.

```powershell
.\scripts\smoke\Run-Smoke.ps1 -BaseUrl https://127.0.0.1:5067 -AdminPass "<local-test-password>"
```

## Incident Notes

Common operational issues:

- Missing or wrong certificate configuration.
- Missing data-protection key.
- Evidence storage permission problems.
- Database file locked or unavailable.
- Manual launcher interrupted before port `5067` starts listening.

## Maturity Notes

Current operations are suitable for a small controlled environment. A more mature setup would include centralized logging, external monitoring, alert notifications, and tested restore drills.
