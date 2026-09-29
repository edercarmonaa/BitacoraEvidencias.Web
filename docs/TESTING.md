# Testing / Pruebas

## English Summary

Testing combines automated .NET tests with operational smoke tests. Automated tests cover application behavior; smoke tests validate deployed workflows.

## Automated Tests

The test project covers areas such as:

- Application startup.
- Service behavior.
- Page model workflows.
- Photo storage behavior.
- Audit logging.
- Security refresh behavior.

Run:

```powershell
dotnet test
```

## Smoke Tests

Smoke scripts validate end-to-end behavior against a running instance:

- Anonymous access redirects.
- Login and logout.
- Role-sensitive behavior.
- Record/case/evidence workflows.
- Evidence authorization.
- Audit log visibility.

Example:

```powershell
.\scripts\smoke\Run-Smoke.ps1 -BaseUrl https://127.0.0.1:5067 -AdminPass "<local-test-password>"
```

## Test Data

Use synthetic records only. Never run public smoke tests against real institutional data.

## Before Publishing

- Run automated tests if the local environment is healthy.
- Confirm no generated test output is staged for Git.
- Confirm no smoke result files contain internal addresses or usernames.
