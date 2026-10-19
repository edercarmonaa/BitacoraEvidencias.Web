# Indice de documentacion

Revision: 19/10/2026. [SQL activo en produccion](CORTE-PRODUCTIVO-0.2.0.md), puerto 5067, ingreso/consultas confirmados y tareas SQL habilitadas. SQLite esta detenida y conservada. Queda pendiente CHECKDB de la copia restaurada por falta de RAM, ademas de los limites documentados de aceptacion/recuperacion.

## Guias vigentes de SQL Server

| Documento | Contenido |
| --- | --- |
| [Estado de 0.2.0](ESTADO-0.2.0.md) | Lo probado, lo aceptado por el responsable y los pendientes. |
| [Corte productivo](CORTE-PRODUCTIVO-0.2.0.md) | Respaldo final, importacion/verificacion, arranque SQL y avance real de la ventana. |
| [Scripts de 0.2.0](SCRIPTS-0.2.0.md) | Compatibilidad, bloqueos SQLite, release/arranque SQL y smokes actualizados. |
| [Acuerdos de migracion](MIGRACION-0.2.0.md) | Conservacion de datos/secretos, corte productivo y recuperacion. |
| [Preparacion](SETUP.md) | Configuracion externa, requisitos y copias. |
| [Ensayo SQL](ENSAYO-SQLSERVER.md) | Preflight, import, verify, publicaciones y arranque. |
| [Migraciones](Database-Migrations.md) | Contextos/historiales separados y limites del importador. |
| [Despliegue](DEPLOYMENT.md) | Publicacion de ensayo y trabajo pendiente para produccion. |
| [Arranque](ARRANQUE-MANUAL.md) | Ensayo SQL frente al arranque productivo SQLite conservado. |
| [Arquitectura](ARCHITECTURE.md) | Componentes, datos, consultas y control local. |
| [Operacion](OPERATIONS.md) | Revision diaria y rutas por entorno. |
| [Backup](BACKUP-OPERACION.md) | BAK/fotos/historicos, credenciales y tarea 14:00. |
| [Auditoria y monitor](AUDITORIA-MONITOREO-0.2.0.md) | Retencion 30 dias, tareas 09:20/09:15 y alertas. |
| [Recuperacion SQL](RECUPERACION-SQLSERVER.md) | Restaurar/trasladar datos, archivos, cuentas y secretos. |
| [Validaciones](VALIDACIONES-0.2.0.md) | Campos, historicos incompletos, toast y confirmaciones. |
| [Rendimiento](PERFORMANCE-0.2.0.md) | Paginacion, indices y mediciones SQL. |
| [Pruebas](TESTING.md) | Resultados por etapa, fallos conocidos y limites. |
| [Smoke](SMOKE-TEST-INTEGRAL.md) | Aceptacion funcional sobre copias. |
| [Cifrado](CIFRADO-DATOS-EVIDENCIAS.md) | Esquema conservado y advertencias del procedimiento SQLite. |
| [HTTPS](HTTPS-LAN-TRANSICION.md) | Alcance de los certificados y diferencia entre HTTPS y TLS SQL. |
| [Seguridad](SECURITY.md) / [Privacidad](PRIVACY-DATA.md) | Custodia, archivos sensibles y revision de publicacion. |
| [Pendientes SGSI](PENDIENTES-SGSI.md) | Controles de 0.2.0 por verificar. |
| [GitHub](GITHUB-PUBLISH-CHECKLIST.md) | Revision antes de publicar; no equivale a despliegue. |

## Referencias SQLite e historicas

| Documento | Alcance |
| --- | --- |
| [SQLite conservada](SQLITE-0.1.md) | Codigo congelado, scripts y consulta en worktree separado. |
| [Backup SQLite](BACKUP-SQLITE-0.1.md) | Copia del procedimiento anterior. |
| [Operacion diaria antigua](OPERACION-DIARIA.md) | Registro SQLite; no ejecutar como guia SQL. |
| [Traslado SQLite](MIGRACION-OTRA-PC.md) | Traslado de archivos .db; para SQL usar recuperacion SQL. |
| [Baseline 27/02](BASELINE-OPERATIVA-2026-02-27.md) | Resultado de una version anterior, no aceptacion SQL. |
| [QA 27/02](QA-CHECKLIST-FUNCIONAL-2026-02-27.md) | Checklist historico, no evidencia actual. |
| [Rendimiento SQLite](RENDIMIENTO-BUSQUEDA.md) | Medicion anterior con ocho casos; no aplicable a SQL. |

Informes internos y resultados de smoke no versionados conservan su fecha y contexto; no se reescriben para presentarlos como pruebas SQL actuales. Antes de publicar, revisar tambien el contenido historico y el historial de Git. El indice no incluye licencias de terceros como procedimientos operativos.
