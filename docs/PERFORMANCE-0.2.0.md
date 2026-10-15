# Paginacion y rendimiento de 0.2.0

Estado: codigo y pruebas locales preparados. Los indices y los tiempos reales deben validarse en SQL Server de pruebas antes del cambio productivo.

Medicion del ensayo `App-pagination-fix`: Auditoria 275 ms de handler (192 ms SQL, cinco oficios); Busquedas 25059 ms de handler y 25042 ms SQL.

## Correccion y diagnostico de Busquedas

La demora se reprodujo ejecutando el handler real sobre SQL Server de pruebas con identidad Windows: conteo 5-6 ms, pagina 25029-25039 ms. Durante la consulta de pagina, `sys.dm_exec_requests` mostro `RESOURCE_SEMAPHORE`, CPU 0 ms y ningun bloqueador: espera por concesion de memoria.

La consulta ahora ordena/pagina solo identificadores y despues carga los campos visibles para esos identificadores (maximo 100), restaurando el orden exacto en memoria. Dos sentencias separadas evitan que el optimizador vuelva a incluir campos anchos/cifrados en el ordenamiento de paginacion. El conteo, filtros y descifrado limitado a la pagina se conservan. Una modificacion concurrente puede cambiar resultados entre consultas, igual que antes entre conteo y pagina; no se incorporan bloqueos largos para este listado.

Medicion real despues: handler 347 ms primera carga y 27 ms segunda; 893 casos totales y 20 visibles. En la segunda carga: conteo 3 ms, identificadores 3 ms, datos 15 ms. Planes: espera de memoria 0 ms, concesion de 1504 KB para identificadores y 1024 KB para datos, sin derrames a disco. Estas mediciones corresponden al handler sin filtros, no al tiempo completo de navegador ni a todos los filtros posibles. Falta la comprobacion interactiva en `App-search-fix`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlServer.ps1 -AppFolder App-search-fix -TrustLocalSqlCertificate
```

El modo `profile` ejecuta el handler real sin filtros, pagina 1 de 20 filas, dos veces sobre `BitacoraEvidencias_Pruebas`. Informa conteo, identificadores y datos por separado y vuelve a ejecutar cada consulta con `STATISTICS XML` para resumir compilacion, ejecucion, memoria, esperas y derrames a disco. No imprime ni guarda SQL, planes completos, parametros, datos personales o credenciales. Consume los resultados en memoria y no cambia registros ni esquema; solo activa estadisticas en su propia sesion. Rechaza `-Production`.

La herramienta debe compilarse en Release desde el repositorio actualizado (no requiere publicar otra carpeta de la aplicacion):

```powershell
dotnet build tools/BitacoraMigration/BitacoraMigration.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/migration/Invoke-BitacoraSqlMigration.ps1 -Mode profile -TrustLocalSqlCertificate
```

Usar la misma consola con la llave de proteccion existente en el entorno y credenciales SQL capturadas localmente. El plan requiere permiso `SHOWPLAN`; si falla se informa solo codigo y estado SQL. Cada consulta conserva un tiempo limite; el diagnostico puede tardar mas de un minuto si reproduce la demora. Compartir solamente la salida resumida. Si la demora solo ocurre con filtros, el diagnostico sin filtros es una referencia inicial y habra que reproducir despues la misma seleccion sin registrar valores sensibles.

El ejecutable admite conexion integrada Windows solo para `profile` mediante la variable de proceso `BITACORA_MIGRATION_SQL_CONNECTION`; el wrapper conserva el ingreso local de usuario/contrasena SQL. Importacion, verificacion y actualizacion mantienen su requisito de autenticacion SQL.

Validacion final: 83 pruebas ejecutadas, 77 aprobadas y los mismos seis fallos previos de almacenamiento/preparacion SQLite. Las nuevas pruebas comprueban orden ascendente/descendente, desempates y resultados vacios con la consulta dividida, ocultamiento de texto/parametros del plan y rechazo de produccion. Diez ejecuciones de wrappers con credenciales ficticias aprobadas. Diagnostico real SQL de solo lectura completado.

## Revision de modulos

| Modulo | Tratamiento |
| --- | --- |
| Busquedas | Conteo e identificadores paginados en SQL; carga acotada de datos por identificadores con orden restaurado. Filtros protegidos por hash y descifrado solo de la pagina visible. |
| Oficios | Conteo y pagina en SQL; orden por fecha e identificador. Los conteos de casos/evidencias se proyectan solo para la pagina. |
| Detalle de oficio | Lista limitada por la regla existente de un caso por nivel educativo (dos niveles); no se cargan fotografias en esa lista. |
| Detalle de caso | Galeria paginada en SQL, 20 archivos por defecto y maximo 100; conteo total independiente de la pagina. |
| Auditoria | Vista de cinco oficios; seleccion fuera de la vista inicial conservada mediante subconsulta, sin consulta separada de identificadores. |
| Auditoria / Mostrar todos | Paginacion SQL con orden unico; evita repetir el conteo si no hay filtro. |
| Reportes / resumen | Agregaciones y conteos en SQL; vistas previas limitadas antes de materializar. |
| Reportes / evidencia incorrecta | Conteo y paginacion en SQL; se elimino la carga completa seguida de Skip/Take en memoria. |
| Reportes / pendientes y no completados | Paginacion SQL y desempate por identificador para fechas iguales. |
| Reportes / tipo de correccion | Paginacion de agregados SQL; tipo de correccion desempata el orden. |
| Reporte ejecutivo | Detalle pendiente paginado en SQL con orden unico; resumenes agrupados siguen calculandose completos en SQL para conservar totales. |
| Usuarios y bitacora | Paginacion SQL, orden unico, limites y cancelacion. |
| Importaciones | Vista previa paginada del archivo ya validado; se conserva la validacion completa del archivo antes de importar. |
| Inicio | Conteos y vistas previas limitadas; consultas secuenciales sobre el mismo contexto. |
| SQL administrativo | Mantiene el limite de lectura de resultados existente; no reescribe sentencias de mantenimiento. |

Los enlaces y formularios GET HTMX comparten sincronizacion para sustituir lecturas anteriores al navegar o cambiar de pagina. Las operaciones POST no se incorporan a ese grupo de cancelacion.

Se mantienen los enlaces de primera/ultima pagina y salto directo; por ello se conserva Skip/Take con orden unico e indices, en lugar de cambiar la navegacion a cursores que impedirian esos saltos.

## Indices SQL

La migracion `PagingIndexes` contiene exclusivamente ocho operaciones CreateIndex: fecha/numero de oficio, oficio/estado de auditoria, estatus/fecha de creacion de caso, fecha de creacion de caso, fecha de verificacion de caso, fecha/identificador de bitacora, usuario/identificador y caso/fecha/identificador de evidencia. Se conserva el indice anterior de relacion de evidencia para que la actualizacion sea estrictamente aditiva.

El importador inicial de una base vacia crea el modelo completo y registra ambas migraciones. Para la base de ensayo ya importada se usa `upgrade`, que exige el historial inicial existente y solo permite esta migracion aditiva. No crea bases, reimporta ni modifica filas de negocio.

1. Detener solo el ensayo del puerto 5068 con Ctrl+C.
2. Aplicar los indices con credenciales SQL locales:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/migration/Invoke-BitacoraSqlMigration.ps1 -Mode upgrade -TrustLocalSqlCertificate
```

3. Solo si termina correctamente, arrancar los binarios nuevos:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlServer.ps1 -AppFolder App-pagination-fix -TrustLocalSqlCertificate
```

4. Recargar con Ctrl+F5 y comprobar filtros, orden, primera/ultima pagina, paginas sucesivas sin duplicados, seleccion de auditoria y galeria. No ejecutar import sobre una base con datos.

El usuario de operacion de la aplicacion sigue siendo distinto de la cuenta administradora usada para preparar el esquema. El switch de confianza es para el certificado SQL local del ensayo; resolver su confianza antes del cambio productivo.

## Mediciones

La consola muestra `Pagina /Busqueda: handler ... ms; comandos ..., SQL ... ms, conexion ... ms`. No se registran texto SQL, parametros, valores de filtros, rutas con identificadores ni credenciales. El tiempo del handler incluye consulta y materializacion; las duraciones SQL corresponden a ejecucion del comando y pueden excluir lectura posterior. Los acumulados SQL/conexion pertenecen al contexto de la solicitud e incluyen la comprobacion de usuario previa al handler. El renderizado y la descarga de imagenes no forman parte del tiempo del handler.

Registrar al menos tres cargas de Busqueda y Auditoria tras aplicar indices, junto con primera y segunda pagina y cambio de modulo. Conservar la primera carga por separado de las posteriores: puede incluir compilacion de consultas y apertura inicial. No se promete una reduccion concreta de milisegundos antes de medir en el servidor.

Pruebas locales: 76 ejecutadas, 70 aprobadas y los mismos seis fallos previos de almacenamiento/preparacion SQLite. Las pruebas nuevas cubren paginacion, filtros, limites, cancelacion, descifrado limitado a la pagina y migracion exclusivamente de indices. Ademas paso una prueba de traduccion de filtros y paginacion del reporte directamente al dialecto SQL Server, sin conexion a la base. Ocho escenarios de los wrappers PowerShell aprobados con credenciales ficticias, sin conexiones SQL reales.

Referencias: [paginacion de EF Core](https://learn.microsoft.com/en-us/ef/core/querying/pagination), [indices](https://learn.microsoft.com/en-us/ef/core/modeling/indexes) y [sincronizacion de solicitudes HTMX](https://htmx.org/attributes/hx-sync/).
