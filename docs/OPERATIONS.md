# Operacion diaria SQL Server 0.2.0

Estado: ensayo SQL; produccion SQLite permanece sin sustituir. [OPERACION-DIARIA.md](OPERACION-DIARIA.md) es el registro historico SQLite, no el procedimiento para los binarios SQL.

| Tarea | Hora de Mexico | Estado |
| --- | --- | --- |
| Monitoreo SQL | 09:15 | Registrador preparado; tarea aun no registrada/activada. |
| Archivado SQL | 09:20 | 30 dias en SQL; historicos sin eliminacion automatica. Registrador preparado. |
| Respaldo SQL | 14:00 | Cierre/respaldo/reinicio en ventana sin usuarios; registrador preparado. |

Las tareas actuales SQLite siguen intactas. Las SQL se crean deshabilitadas y se activan solo durante el cambio productivo. Sesion Windows abierta, aunque bloqueada; credenciales DPAPI de la misma cuenta/equipo.

## Revision diaria

1. Comprobar Login HTTPS con nombre cubierto por el certificado y conexion del usuario SQL de operacion.
2. Revisar latest-status/latest-run de monitoreo y archivado, y los logs de respaldo. Un codigo no cero exige revision; no asumir exito por existir cualquier ZIP.
3. Comprobar respaldo completo de la base correcta: BAK, evidencias, historicos y manifiesto. Mantener la llave/certificado/credenciales bajo custodia separada.
4. Revisar espacio y permisos. El ultimo ensayo encontro unos 23 GB en C frente al umbral normal de 25 GB; el dato es una medicion de ese momento, no una consulta actual.
5. Revisar conteos de eventos y archivos historicos. No borrar historicos para cumplir la retencion de respaldos: son politicas distintas.

## Rutas

| Datos | Produccion prevista SQL | Ensayo |
| --- | --- | --- |
| Base | .\Auditoria / BitacoraEvidencias | .\Auditoria / BitacoraEvidencias_Pruebas |
| Evidencias | C:\BitacoraEvidencias\Evidencias | C:\BitacoraEvidencias_Pruebas\Input\Evidencias |
| Historicos | C:\BitacoraEvidencias\AuditArchive\SqlServer\BitacoraEvidencias | Raiz AuditArchive de pruebas / SqlServer / base de pruebas |
| Monitoreo | C:\BitacoraEvidencias\Monitoreo\SqlServer\BitacoraEvidencias | Raiz Monitoreo de pruebas / SqlServer / base de pruebas |
| Respaldo | BACKUP_ROOT\SqlServer\BitacoraEvidencias | BACKUP_ROOT\SqlServer\BitacoraEvidencias_Pruebas |

Mantener ademas los archivos diarios SQLite de AuditArchive; el respaldo completo SQL los incluye. Si se cambia ArchiveRoot, pasar la misma raiz al backup mediante AuditArchiveRoot. No ejecutar archivado y backup simultaneamente.

Los comandos completos y preparacion de credenciales/permisos estan en [BACKUP-OPERACION.md](BACKUP-OPERACION.md) y [AUDITORIA-MONITOREO-0.2.0.md](AUDITORIA-MONITOREO-0.2.0.md). No repetir import, activar reset, renovar llave ni ejecutar smokes con escrituras en produccion para resolver una incidencia.

Arranque de ensayo: [ARRANQUE-MANUAL.md](ARRANQUE-MANUAL.md). Publicacion/cambio pendiente: [DEPLOYMENT.md](DEPLOYMENT.md). Recuperacion: [RECUPERACION-SQLSERVER.md](RECUPERACION-SQLSERVER.md).
