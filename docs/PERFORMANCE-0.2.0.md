# Paginacion y rendimiento de 0.2.0

Estado: codigo y pruebas locales preparados. Los indices y los tiempos reales deben validarse en SQL Server de pruebas antes del cambio productivo.

## Revision de modulos

| Modulo | Tratamiento |
| --- | --- |
| Busquedas | Conteo y pagina en SQL, orden unico y cancelacion; los filtros protegidos conservan la busqueda exacta por hash y solo se descifra la pagina visible. |
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
