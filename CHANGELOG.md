# Historial de cambios

## [Sin publicar]

### 0.2.0 - SQL Server (en preparacion)

- Correccion del panel posterior al login: esperar cada consulta para evitar operaciones concurrentes sobre el mismo DbContext; prueba con lector asincrono demorado.

- Contexto y migracion inicial EF Core independientes para SQL Server Express.
- Importador transaccional desde una copia SQLite: conserva identificadores, hashes de contrasenas, sales, cifrado y auditoria; compara todos los campos antes de confirmar.
- Comprobacion de integridad, relaciones, longitudes, archivos y descifrado con la llave existente; rechazo de destinos no vacios.
- Extraccion del respaldo con comparacion SHA256 y lanzadores independientes para ensayo en el puerto 5068.
- Conservacion de SQLite en la etiqueta local `v0.1.0` y la rama `archive/sqlite-0.1`; desarrollo SQL en `feature/sqlserver-0.2`.
- La version aun no esta aprobada para sustituir produccion. Consultar `docs/ENSAYO-SQLSERVER.md`.

## [0.1.0] - 2026-10-14

Primera version numerada de la aplicacion existente, en desarrollo.

### Funcionalidades incluidas

- Registro de oficios, casos de correccion y evidencias fotograficas.
- Autenticacion, roles, administracion de usuarios y bitacora de auditoria.
- Revision de evidencias, busqueda, reportes e importacion tabular.
- Persistencia en SQLite y almacenamiento local de evidencias con miniaturas.
- Scripts de preparacion, respaldo, monitoreo y despliegue local.

### Versionado

- Version explicita `0.1.0` en el proyecto .NET para identificar los binarios publicados.
- Historial de cambios y procedimiento de etiquetado en Git.
