# Historial de cambios

## [Sin publicar]

### 0.2.0 - SQL Server (desplegada; recuperacion integral pendiente)

- Corte productivo iniciado el 19/10/2026: respaldo final SQLite, importacion/verificacion SQL y arranque en IP del equipo/5067; detalle y aceptacion pendientes en docs/CORTE-PRODUCTIVO-0.2.0.md. Lanzador instalado con configuracion DPAPI y cuenta de respaldos separada autorizada; diagnostico de BACKUP/VERIFYONLY/HEADERONLY diferenciado.
- Pruebas de almacenamiento aisladas de la clave real y ampliadas a rutas antiguas/protegidas, lectura historica y reversiones. Bootstrap SQLite probado mediante compatibilidad explicita y rechazo sin ella, sin alterar migraciones congeladas. Suite completa Debug: 132 aprobadas, cero fallos/omitidas.
- Revision de scripts: bloqueo previo de preparacion/reset/stamp/publicacion SQLite en la rama SQL; reconocimiento del binario SQLite instalado previo a la numeracion y retiro de taskkill por nombre del arranque manual.
- Publicador SQL independiente y lanzador productivo con validacion sin arranque, credenciales locales y entorno restaurado; sin activar el cambio productivo.
- Smokes actualizados a campos obligatorios, fechas de Mexico y carpetas HMAC; objetivos locales no productivos, escrituras explicitas, TLS estricto por defecto, archivos curl privados y salida con error ante FAIL. Ambos recorridos SQL sinteticos: 47 PASS y un SKIP cada uno.
- Certificados sin sobrescritura ni contrasena en consola, credencial HTTPS DPAPI y Tailwind fijado a 4.2.1 como el CSS existente; compilacion v4 mediante @config e informe de fallos. Sin renovar certificados ni descargar assets durante la revision.

- Documentacion revisada el 19/10/2026: indice, estado de aceptacion, configuracion/arranque SQL, recuperacion, seguridad, pruebas y referencias historicas SQLite. Se corrigen instrucciones de inicializacion incompatibles y pendientes ya resueltos; se mantiene pendiente el cambio productivo.

- Auditoria/monitoreo adaptados a SQL Server con credenciales DPAPI y reportes separados por base; originales SQLite conservados. Archivado de mas de 30 dias en lotes verificados, sin sobrescritura y retirada transaccional por identificadores.
- Monitoreo de conexion/esquema SQL, tamanos de datos/log y respaldos completos con manifiesto; errores/alertas devuelven fallo sin mensajes del proveedor. Preparacion de tareas SQL deshabilitadas, conservando 09:15 y 09:20.
- Respaldo completo ampliado a historicos de auditoria SQLite/SQL y coordinado con el archivador mediante bloqueo SQL, para conservar registros retirados de la base.

- Respaldo SQL nativo COPY_ONLY/CHECKSUM, VERIFYONLY, evidencias y manifiesto SHA256 en ZIP; retencion aislada de los respaldos SQLite. Credencial SQL protegida con DPAPI para tareas sin entrada interactiva.
- Preparacion de tarea SQL diaria a las 14:00 de Mexico, inicialmente deshabilitada; cierre ordenado y reinicio mediante control local de la misma cuenta Windows, conservando configuracion solo en memoria. La tarea productiva SQLite no se modifica.
- Script y guia SQLite conservados por separado; alerta de oficio duplicado simplificada a "Ya existe un oficio con ese numero."

- Formularios: validacion al salir del campo, errores junto al control, resumen en toast y confirmaciones con Bootstrap local; integracion HTMX y mensajes predeterminados en espanol.
- Reglas reforzadas de CURP/CCT (incluidas claves antiguas), periodos y fechas; folio/validador opcionales, historicos incompletos sin cambios permitidos y unicidad de oficio sin distinguir mayusculas en altas/ediciones/importaciones.
- Adjuntos con comprobaciones anticipadas de extension/tamano y validacion de servidor conservada; errores de servidor asociados al campo correcto.
- Busquedas: corregida espera RESOURCE_SEMAPHORE reproducida en SQL Server de pruebas; paginar identificadores antes de cargar campos visibles reduce el ancho del ordenamiento. Medicion del handler sin filtros: 25 segundos antes, 347 ms primera carga y 27 ms segunda carga despues; funcionamiento confirmado posteriormente por el responsable en el ensayo.
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
