# Arquitectura de 0.2.0

Aplicacion monolitica ASP.NET Core Razor Pages con EF Core 9/SQL Server Express 2025 y evidencias en disco. SQLite 0.1.0 permanece congelada en Git y en la instalacion productiva hasta aprobar el cambio. La arquitectura original buscaba operar en una PC Windows compartida de pocos recursos; SQL Express agrega un servicio local, sin introducir contenedores ni una API separada.

## Componentes

| Componente | Funcion |
| --- | --- |
| Pages / ViewComponents | Flujos Razor, autorizacion, formularios y panel. |
| Services | Casos/oficios/evidencias, importaciones, auditoria, almacenamiento y tiempos de consultas. |
| Security / Validation | Hash de contrasenas, roles, cifrado y reglas compartidas. |
| SqlServerAppDbContext | Contexto operativo SQL, migraciones/indices propios, registrado tambien como AppDbContext. |
| Data/Migrations | Modelo e historial SQLite conservados; no son migraciones SQL. |
| Data/SqlServerMigrations | InitialSqlServer y PagingIndexes. |
| tools/BitacoraMigration | Transferencia desde copia SQLite, comparacion y diagnostico. |
| scripts/operations | Consultas/archivado SQL y preparacion de tareas sin secretos en argumentos. |
| wwwroot | Bootstrap, HTMX, jQuery Validation y recursos locales. |

## Datos y consultas

SQL conserva registros, usuarios, hashes/sales, roles, referencias de imagenes y auditoria. Archivos originales y miniaturas quedan fuera de SQL. El importador transfiere valores fisicos sin SaveChanges para evitar recifrado o nuevos eventos, conserva identidades y verifica todos los campos antes del commit.

La configuracion SQL usa intercalacion binaria para preservar diferencias del origen. La unicidad de numero de oficio sin distinguir mayusculas se valida en rutas de aplicacion mediante transacciones; no significa que toda la base tenga intercalacion insensible a mayusculas.

Los listados tienen conteo y paginacion SQL con orden estable. Busquedas pagina primero identificadores y despues carga los campos visibles para evitar ordenar columnas anchas/cifradas. El descifrado se limita a la pagina. Un mismo DbContext no ejecuta consultas concurrentes. HTMX cancela lecturas GET sustituidas sin incorporar POST a ese grupo.

## Interfaz y proteccion

Los formularios validan al salir del campo y al guardar, con errores locales, toast y confirmaciones Bootstrap. El servidor conserva las reglas aunque JavaScript no este disponible. Los datos historicos incompletos sin cambios siguen siendo editables conforme a las reglas documentadas en [VALIDACIONES-0.2.0.md](VALIDACIONES-0.2.0.md).

La llave externa protege campos/archivos y genera hashes de busqueda; se mantiene durante migracion y restauracion. Fotografias cifradas se sirven por endpoints autenticados. No se almacenan fotos en SQL ni secretos en Git.

## Operacion

Backup: BAK COPY_ONLY/CHECKSUM, VERIFYONLY, fotos/historicos y ZIP con SHA256. Archivo de auditoria: lotes JSONL verificados antes de retirar identificadores antiguos. Monitor: consultas SQL agregadas, HTTP, disco y respaldo. Coordinacion backup/archivado mediante bloqueo SQL de aplicacion.

LocalBackupControlService usa una canalizacion Windows accesible a la misma identidad para cierre ordenado y obtencion temporal en memoria de la configuracion de reinicio; no agrega una ruta HTTP. Funciona con el host independiente, no con IIS/servicios Windows. La configuracion no persiste si se interrumpe la tarea o Windows; recuperar con el arranque habitual.

Los scripts nuevos no implican despliegue productivo realizado. La custodia de secretos, recuperacion, permisos y aceptacion siguen documentados en [ESTADO-0.2.0.md](ESTADO-0.2.0.md).
