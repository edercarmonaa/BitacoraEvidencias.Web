# Architecture / Arquitectura

## English Summary

Evidence Log System follows a pragmatic monolithic architecture: one ASP.NET Core Razor Pages web application, a SQLite database accessed through EF Core, filesystem-backed evidence storage, and PowerShell scripts for operational tasks.

The design favors simplicity, auditability, and local operability over distributed complexity.

## Resumen en Espanol

Evidence Log System usa una arquitectura monolitica pragmatica: una aplicacion web ASP.NET Core Razor Pages, una base SQLite mediante EF Core, almacenamiento de evidencias en filesystem y scripts PowerShell para tareas operativas.

La prioridad es mantener el sistema comprensible, auditable y operable en un entorno institucional pequeno o mediano.

## Constraints-Driven Architecture

This architecture was selected for a constrained physical and operational environment, not for an idealized cloud or server-room scenario.

The target deployment was a Windows 10 desktop PC with a Celeron-class processor, 4 GB of RAM, a 500 GB HDD, and regular office usage happening on the same machine. The system also had to operate on a simple internal LAN without assuming a dedicated database server, container platform, centralized monitoring stack, load balancer, or full-time infrastructure support.

Several important constraints were outside the direct scope of the software implementation:

- Available hardware.
- Internal network characteristics.
- Lack of dedicated database/server infrastructure.
- Budget and procurement limitations.
- Local Windows operational practices.
- Need for maintainability by non-specialized operators.

Because of that, the design deliberately favors a small operational footprint:

- Server-rendered Razor Pages instead of a heavier SPA.
- SQLite instead of SQL Server, PostgreSQL, or MySQL.
- Filesystem evidence storage instead of binary blobs inside the database.
- PowerShell scripts instead of a separate automation platform.
- Direct .NET deployment instead of Docker-based hosting.
- Local monitoring and backup scripts instead of centralized observability.

From a portfolio perspective, this is one of the most important lessons in the project: professional architecture is not only the ability to use sophisticated infrastructure. It is also the ability to design responsibly for the machine, network, permissions, budget, and people that actually exist.

## Arquitectura Guiada por Restricciones

Esta arquitectura se eligio para un entorno fisico y operativo limitado, no para un escenario ideal de nube o sala de servidores.

El despliegue objetivo era una PC de escritorio con Windows 10, procesador tipo Celeron, 4 GB de RAM, disco HDD de 500 GB y uso simultaneo para tareas normales de oficina. El sistema tambien debia funcionar en una LAN interna simple, sin asumir servidor dedicado de base de datos, plataforma de contenedores, monitoreo centralizado, balanceador o soporte permanente de infraestructura.

Varias restricciones estaban fuera del alcance directo del desarrollo:

- Hardware disponible.
- Caracteristicas de la red interna.
- Ausencia de infraestructura dedicada para base de datos o servidor.
- Limitaciones de presupuesto y adquisicion.
- Practicas operativas locales en Windows.
- Necesidad de mantenimiento por personal no especializado.

Por eso, el diseno favorece una huella operativa pequena:

- Razor Pages server-rendered en lugar de una SPA pesada.
- SQLite en lugar de SQL Server, PostgreSQL o MySQL.
- Evidencias en filesystem en lugar de binarios dentro de la base de datos.
- Scripts PowerShell en lugar de una plataforma separada de automatizacion.
- Despliegue directo con .NET en lugar de Docker.
- Monitoreo y respaldo locales en lugar de observabilidad centralizada.

Desde la perspectiva de portafolio, este es uno de los aprendizajes principales del proyecto: la arquitectura profesional no consiste solamente en usar infraestructura sofisticada. Tambien consiste en disenar responsablemente para la maquina, la red, los permisos, el presupuesto y las personas que realmente existen.

## High-Level Components

- **Pages/**: Razor Pages for UI workflows such as login, reports, administrative tools, cases, records, and evidence management.
- **Services/**: Application services that coordinate business workflows, validation, import previews, storage operations, auditing, and user administration.
- **Data/**: EF Core `AppDbContext`, migrations, SQLite pragmas, and persistence configuration.
- **Models/**: Domain entities for users, records, cases, evidence, corrections, catalogs, and audit logs.
- **Security/**: Password hashing, sensitive-data protection, audit event constants, and user-session refresh behavior.
- **scripts/**: Operational automation for backup, deployment, HTTPS startup, monitoring, database checks, smoke tests, and audit archiving.
- **tools/**: Small supporting .NET utilities used by operational scripts.
- **tests/**: Automated tests for services, page models, startup behavior, and security behaviors.

## Data and Storage

SQLite is used as the relational database because the target scenario is a controlled internal deployment with modest concurrency, simple backup requirements, and operational preference for a single-file database.

Evidence images are stored outside the database. The database keeps metadata and relative paths, while the filesystem stores originals and generated thumbnails. This keeps the database small and makes evidence backup/restore strategies explicit.

## Request Flow

1. A user authenticates through Razor Pages.
2. Authorization policies and roles restrict administrative and operational workflows.
3. Page models call application services instead of embedding workflow logic in UI code.
4. Services coordinate EF Core persistence, file storage, validation, and audit logging.
5. Important actions write audit entries for later review and archival.

## Why Razor Pages

Razor Pages fits this project because the workflows are page-oriented: create/edit/delete records, upload evidence, review audit logs, import files, and run administrative screens. It keeps server-side validation and authorization close to each workflow without adding SPA infrastructure.

It also reduces client-side complexity and runtime overhead, which matters when the application must run from a low-resource desktop PC shared with office tasks.

## Why EF Core and SQLite

EF Core provides migrations, typed queries, model configuration, and testable persistence behavior. SQLite keeps deployment simple for a small internal system and allows file-level backup strategies. For larger concurrency needs, the persistence layer could evolve toward a server database.

SQLite was selected because no dedicated database server was available in the target environment. It avoided adding another service to install, secure, monitor, and troubleshoot on constrained hardware.

## Why PowerShell

The operational environment is Windows-oriented. PowerShell provides repeatable scripts for daily work without requiring a separate orchestration platform. Scripts are used for backup, deploy, HTTPS startup, monitoring, and smoke testing.

## Why Not Docker

Docker was not rejected as a technology. It was not the best fit for the original deployment conditions: a low-resource Windows 10 desktop PC, shared office usage, and a need for direct, understandable operation by local staff. Direct .NET execution plus PowerShell scripts created fewer moving parts and a smaller support burden.

In a different environment with stronger hardware and dedicated infrastructure, containerization could be a reasonable evolution.

## Boundaries and Trade-Offs

- The system is intentionally monolithic.
- It avoids a separate API layer because the current UI and workflows are server-rendered.
- Evidence files are not stored in the database, which improves database size and backup flexibility but requires filesystem permissions and backup discipline.
- Monitoring is script-based and local. A more mature production setup would add centralized telemetry and external alerting.
- The architecture optimizes for the actual deployment environment rather than for horizontal scalability.
