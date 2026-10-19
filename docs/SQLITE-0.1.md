# SQLite 0.1.0 conservada

La version SQLite sigue siendo la referencia productiva hasta el cambio aprobado. Codigo, scripts y documentacion originales estan en v0.1.0 y archive/sqlite-0.1 (6aba98e). No mover ni recrear esas referencias. No hacer checkout de otra version en la carpeta a la que apunta el acceso directo productivo.

Para consultar/compilar por separado:

```powershell
git worktree add --detach C:\Repos\BitacoraEvidencias.SQLite v0.1.0
```

Usar otra ruta si ya existe. No iniciar esa copia sobre datos productivos para probarla. La etiqueta guarda codigo, no bases, evidencias, variables, certificados o llaves. Mantener respaldo externo del conjunto y la misma llave.

## Scripts

| Flujo | Estado en feature/sqlserver-0.2 |
| --- | --- |
| Backup SQLite | Backup-BitacoraSqlite.ps1 conserva el script anterior; el principal admite Provider Sqlite. |
| Archivado SQLite | Archive-AuditLogSqlite.ps1 conserva el original; depende de la herramienta SQLite publicada previamente. |
| Monitor SQLite | Monitor-BitacoraSqlite.ps1 conserva el original; depende de la herramienta SQLite previa. |
| Inicializacion/stamp/legacy | scripts/db siguen orientados a SQLite; usar el codigo congelado 0.1.0, no Program.cs SQL. |
| Publicacion/arranque/HTTPS antiguos | Configuran SQLite. No usarlos para preparar SQL ni publicar el codigo 0.2.0 sobre la instalacion anterior. |

[BACKUP-SQLITE-0.1.md](BACKUP-SQLITE-0.1.md), [OPERACION-DIARIA.md](OPERACION-DIARIA.md) y [MIGRACION-OTRA-PC.md](MIGRACION-OTRA-PC.md) son referencias historicas. Las tareas ya instaladas se conservan hasta el corte; no cambiar sus acciones por los nuevos scripts de proveedor SQL prematuramente.

El backup SQLite anterior no incluye por si solo historicos AuditArchive, binarios, certificado ni secretos. Conservar esos elementos mediante custodia/copia separada para la migracion. No confundir una etiqueta de Git o un ZIP de la base con una recuperacion completa.
