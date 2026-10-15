# Ensayo de migracion SQLite 0.1.0 a SQL Server 0.2.0

La nueva aplicacion usa SQL Server. La instalacion SQLite sigue siendo productiva. No usar los scripts historicos de preparacion, publicacion o respaldo SQLite para gestionar SQL Server.

## Codigo conservado

- Etiqueta anotada: `v0.1.0`.
- Rama congelada: `archive/sqlite-0.1`.
- Rama de desarrollo: `feature/sqlserver-0.2`.
- Las referencias son locales hasta publicarlas en GitHub. No crear la etiqueta `v0.2.0` hasta aprobar el ensayo.

Para consultar o compilar SQLite sin cambiar los archivos del repositorio usado por el acceso directo productivo, crear otra carpeta de trabajo:

```powershell
git worktree add --detach C:\Repos\BitacoraEvidencias.SQLite v0.1.0
```

Git conserva codigo, scripts y documentacion. Mantener bases, evidencias, certificados, variables y llaves en respaldos protegidos externos a Git. La etiqueta no contiene esos datos.

## Preparacion

Requisitos: SDK .NET 9, runtime ASP.NET Core 9 y las variables existentes de llave y certificado disponibles en la consola que ejecuta los procedimientos. La politica de ejecucion se puede establecer con `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <script>` solo para ese proceso; no cambiar la politica persistente del equipo.

```powershell
dotnet restore -p:NuGetAudit=false
dotnet build tools/BitacoraMigration/BitacoraMigration.csproj -c Release --no-restore
dotnet test -c Release --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validation/Test-SqlServerConnectionScripts.ps1
```

`NuGetAudit=false` permite restaurar sin el servicio de auditoria disponible durante la preparacion; no constituye una revision de vulnerabilidades. Resolver el acceso al servicio y revisar dependencias antes de liberar.

La suite SQLite tenia seis fallos documentados antes de este cambio. Las pruebas nuevas `SqlServerMigrationTests` validan generacion del esquema SQL, separacion de migraciones, precision de fechas y comparacion de filas. No sustituyen las pruebas reales contra SQL Server ni la aceptacion de los flujos de la aplicacion.

## Restaurar un respaldo para el ensayo

```powershell
.\scripts\migration\Expand-BitacoraRehearsalBackup.ps1 -BackupZip 'RUTA_DEL_RESPALDO.zip'
```

El destino predeterminado es `C:\BitacoraEvidencias_Pruebas\Input`. Rechaza una carpeta Input existente, rutas fuera del destino y archivos duplicados; compara por SHA256 cada archivo extraido con su entrada en el ZIP. No sobrescribe ni elimina copias anteriores.

Ya se extrajo el respaldo `BitacoraBackup_20261014_091416.zip` en esa carpeta y se compararon 3,658 archivos. Es un ensayo del respaldo, no una copia final actualizada de produccion. Registrar aparte el resultado del preflight y del ensayo SQL.

## Preflight sin conexion SQL

```powershell
.\scripts\migration\Invoke-BitacoraSqlMigration.ps1 -Mode preflight
```

Comprueba integridad SQLite, relaciones, coincidencia de tablas y columnas, tipos y longitudes, existencia de usuarios, descifrado de valores y evidencias con la llave actual y presencia de originales y miniaturas. Solo lee la copia SQLite; puede necesitar permiso sobre la carpeta de ensayo para los archivos auxiliares de SQLite. No inicializa SQLite ni modifica su esquema.

Si detecta incompatibilidades, detenerse y revisarlas. No truncar datos, omitir columnas, generar otra llave ni reconstruir usuarios.

## Importar exclusivamente a pruebas

```powershell
.\scripts\migration\Invoke-BitacoraSqlMigration.ps1 -Mode import
.\scripts\migration\Invoke-BitacoraSqlMigration.ps1 -Mode verify
```

Los comandos solicitan usuario y contrasena SQL localmente. La contrasena se introduce sin mostrarse; la cadena se transmite al proceso hijo por una variable de entorno temporal, nunca como argumento. No ejecutar bajo transcripcion o captura de variables de depuracion. Las variables persistentes de Windows no se modifican.

El destino predeterminado y obligatorio sin `-Production` es `.\Auditoria`, base `BitacoraEvidencias_Pruebas`. Requiere base ya existente y sin objetos de usuario para importar. No elimina tablas ni crea bases. El usuario existente debe tener permisos de preparacion del esquema y transferencia.

La conexion exige cifrado y verifica el certificado SQL por defecto. Si el certificado local aun no tiene una cadena de confianza configurada, el parametro explicito `-TrustLocalSqlCertificate` permite el ensayo local sin verificar su identidad; no desactiva el cifrado. Resolver la confianza del certificado antes del cambio productivo.

Se crean tablas e indices, se transfieren valores almacenados sin usar SaveChanges, se conservan identidades y se ajustan sus secuencias. Se compara cada campo de cada fila mediante una huella determinista, incluyendo distincion de nulos y vacios y precision de fechas. Todo se confirma en una sola transaccion; los fallos revierten esquema y datos. El historial EF Core SQL se registra independientemente del historial SQLite.

La transferencia inicial admite `InitialSqlServer` y `PagingIndexes`, registrando ambas sobre el modelo completo. Para la base de ensayo ya importada, aplicar exclusivamente los indices con `-Mode upgrade`: ver [PERFORMANCE-0.2.0.md](PERFORMANCE-0.2.0.md). No repetir import. Ante otras migraciones futuras, revisar el importador antes de usarlas.

`verify` compara contra el respaldo original: ejecutarlo antes de las pruebas de escritura, pues estas modificaran la copia SQL. Tras probar escrituras, conservar los resultados y preparar otra base vacia para repetir; este proceso no incluye borrado automatico.

## Publicar y arrancar el ensayo

```powershell
.\scripts\deploy\Publish-BitacoraSqlServer.ps1
.\scripts\deploy\Start-BitacoraSqlServer.ps1
```

Ambos scripts admiten `-AppFolder` para usar otra carpeta de binarios dentro del ensayo sin sustituir una publicacion en ejecucion. La correccion del panel posterior al login se publica en `App-dashboard-fix`; para usarla, detener solo el ensayo con Ctrl+C y arrancar con `-AppFolder App-dashboard-fix -TrustLocalSqlCertificate` si se usa el certificado SQL local sin confianza configurada. La base y las copias de evidencias se mantienen: no repetir import.

Publica en `C:\BitacoraEvidencias_Pruebas\App` y usa exclusivamente la base de pruebas y `Input\Evidencias`. El puerto predeterminado es 5068; no admite 5067 ni detiene procesos existentes. La cuenta debe poder leer el certificado HTTPS y la llave existentes. Si el certificado no cubre 127.0.0.1, usar `-ServerAddress` con el nombre o direccion cubierto por el certificado. El parametro de confianza SQL tambien esta disponible en el lanzador si se autorizo para el ensayo local.

Validar cuentas existentes, permisos de ambos roles, lectura de datos protegidos, fotografias y miniaturas, busqueda, reportes, auditoria y operaciones de alta, modificacion y eliminacion sobre las copias. Confirmar que se pueden crear nuevos registros sin colision de identificadores.

La optimizacion de Auditoria se publica en `App-audit-fix`. Usar `Start-BitacoraSqlServer.ps1 -AppFolder App-audit-fix -TrustLocalSqlCertificate` despues de detener el ensayo anterior. Verificar entrada, seleccion de casos, busqueda, Mostrar todos y salida a otro modulo. La consola muestra `Auditoria: carga de datos ... ms; oficios cargados ... de ...`; compartir solo esa linea para comparar tiempos, sin credenciales ni registros personales. El resultado en el entorno real sigue pendiente de validar.

## Antes de produccion

El ensayo no autoriza un cambio inmediato de instalacion. Primero resolver los fallos relevantes y aprobar los resultados. Preparar y verificar respaldo/restauracion SQL Server, respaldo de evidencias y custodia externa de secretos; los scripts SQLite existentes no respaldan SQL Server.

Durante mantenimiento: detener todas las escrituras, crear respaldo final consistente y una copia de trabajo de ese respaldo, repetir preflight, importar con `-Production` usando esa nueva copia y validar antes de admitir usuarios. El switch exige confirmacion local; no usar para el ensayo. Mantener intacta la base SQLite y sus binarios.

La publicacion y el lanzador nuevos son exclusivos de ensayo: el cambio productivo de binarios, conexion, puerto y rutas requiere un procedimiento separado tras aprobarlo. No copiar la configuracion de pruebas sobre la configuracion productiva.

Si falla antes de admitir usuarios, detener SQL y restaurar el estado SQLite, archivos y configuracion del respaldo final. Si ya existen escrituras nuevas en SQL, no reiniciar simplemente la base SQLite antigua: preparar conciliacion de esos cambios para evitar perdida de datos.

Referencias tecnicas: [migraciones por proveedor EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers) y [cifrado y validacion de certificados SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation).
