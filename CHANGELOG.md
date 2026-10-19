# Historial de cambios

## [Sin publicar]

### 0.2.0 - SQL Server (en preparacion)

- Formularios: validacion al salir del campo, errores junto al control, resumen en toast y confirmaciones con Bootstrap local; integracion HTMX y mensajes predeterminados en espanol.
- Reglas reforzadas de CURP/CCT (incluidas claves antiguas), periodos y fechas; folio/validador opcionales, historicos incompletos sin cambios permitidos y unicidad de oficio sin distinguir mayusculas en altas/ediciones/importaciones.
- Adjuntos con comprobaciones anticipadas de extension/tamano y validacion de servidor conservada; errores de servidor asociados al campo correcto.
- Busquedas: corregida espera RESOURCE_SEMAPHORE reproducida en SQL Server de pruebas; paginar identificadores antes de cargar campos visibles reduce el ancho del ordenamiento. Medicion del handler sin filtros: 25 segundos antes, 347 ms primera carga y 27 ms segunda carga despues; pendiente confirmacion en navegador.
- Diagnostico `profile` de Busquedas en la base de pruebas: separa conteo, identificadores y datos; resume tiempos, memoria y esperas del plan real sin mostrar consultas, parametros ni registros.
- Revision de paginacion: cancelacion de lecturas GET y orden estable en los listados; galeria de caso paginada y reporte de evidencia incorrecta paginado en SQL.
- Agregaciones de auditoria/reportes en SQL y filtros protegidos de Busquedas conservados, con descifrado limitado a la pagina.
- Ocho indices SQL aditivos mediante PagingIndexes y modo upgrade para bases ya importadas; mediciones por pagina sin valores sensibles.

- Auditoria: limitar la carga y descifrado a los cinco oficios visibles, conservar seleccion fuera de la vista inicial y cancelar lecturas anteriores al navegar; registrar duracion sin datos personales.

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
