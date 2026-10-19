# Respaldos SQL Server Express 0.2.0

Horario acordado: **diariamente a las 14:00, hora de Mexico**, sin usuarios. En el [corte productivo](CORTE-PRODUCTIVO-0.2.0.md), la tarea SQL quedo habilitada y aprobo un ciclo real de cierre/respaldo/reinicio con BitacoraRespaldo, credencial DPAPI separada y resultado 0. La tarea SQLite esta conservada y deshabilitada. Su procedimiento permanece en [BACKUP-SQLITE-0.1.md](BACKUP-SQLITE-0.1.md) y `Backup-BitacoraSqlite.ps1`; `v0.1.0` conserva el codigo original.

## Contenido y verificacion

`scripts/backup/Backup-Bitacora.ps1` ahora usa SQL Server por defecto, instancia local `.\Auditoria`. Genera un respaldo nativo `COPY_ONLY` con `CHECKSUM`, ejecuta `RESTORE VERIFYONLY` y comprueba su cabecera. Rechaza una base sin la migracion SQL inicial.

El ZIP contiene `Data/<archivo>.bak`, `Evidencias/` (originales y miniaturas, conservando su estado cifrado), `AuditArchive/` (historicos diarios SQLite y lotes SQL finalizados de esa base) y `backup-manifest.json` con tamanos y SHA256. Cada archivo se compara al copiarlo y dentro del ZIP. Solo entonces se publica el `.zip` definitivo y se aplica retencion de 30 dias. Los ZIP SQLite, otras bases y archivos incompletos quedan fuera de esa limpieza.

La raiz historica predeterminada es `C:\BitacoraEvidencias\AuditArchive` o `C:\BitacoraEvidencias_Pruebas\AuditArchive` para ensayo. Si el archivador usa otra raiz, pasar `-AuditArchiveRoot` al respaldo y al ejecutor programado. El respaldo obtiene un bloqueo SQL compartido que excluye archivados concurrentes. Ver [AUDITORIA-MONITOREO-0.2.0.md](AUDITORIA-MONITOREO-0.2.0.md).

La carpeta historica debe existir; si falta, revisar la ruta en lugar de generar un respaldo supuestamente completo sin historicos. Crear una carpeta vacia solo si se comprobo que aun no existen archivos historicos.

Destino: `<BITACORA_BACKUP_ROOT>\SqlServer\BitacoraEvidencias`. Se respeta el destino externo configurado; sin parametro ni variable se usa `C:\BitacoraEvidencias\BackupsBitacora`. Comprobar espacio disponible en destino y en TEMP: el empaquetado necesita una copia temporal de la base y las evidencias. La carpeta temporal tiene permisos limitados a la cuenta actual, SYSTEM y administradores. Si falla el empaquetado se conserva el `.bak` nativo; revisar el log antes de limpiar archivos concretos.

**La llave `BITACORA_DATA_PROTECTION_KEY` no se incluye en el ZIP.** Conservar por separado esa misma llave, las credenciales SQL de la aplicacion, certificado HTTPS y contrasena, configuracion y binarios. No regenerar la llave al restaurar. La credencial DPAPI de respaldo tampoco sustituye la configuracion de la aplicacion ni se incluye en el paquete.

## Preparacion local

Ejecutar con la misma cuenta Windows que inicia la aplicacion. Estos pasos se realizan localmente, sin compartir contrasenas ni guardarlas en Git:

```powershell
.\scripts\backup\Initialize-BitacoraSqlBackupDirectory.ps1 -Directory C:\BitacoraEvidencias\SqlBackupStaging
.\scripts\backup\Save-BitacoraSqlBackupCredential.ps1 -CredentialPath C:\BitacoraEvidencias\Secrets\SqlBackup.credential.xml
```

El primer script requiere una carpeta dedicada vacia. Limita permisos a la cuenta de la tarea, el servicio `NT SERVICE\MSSQL$Auditoria`, creador, SYSTEM y administradores. Si el servicio SQL usa otra identidad, revisar los permisos antes de usarlo. SQL Server escribe el `.bak`; la cuenta Windows de la tarea debe poder leerlo y eliminarlo despues de verificar el ZIP.

El segundo solicita usuario/contrasena SQL y los guarda mediante DPAPI: solo la misma cuenta Windows en el mismo equipo puede descifrarlos. No sobrescribe archivos existentes. El usuario SQL necesita permisos para `BACKUP DATABASE` y `RESTORE VERIFYONLY` (este ultimo requiere permiso de creacion de bases segun SQL Server). No se otorgan permisos automaticamente. TLS permanece activado; `-TrustLocalSqlCertificate` es una excepcion explicita para el certificado local sin cadena de confianza.

## Tarea de las 14:00

La publicacion SQL debe incluir `LocalBackupControlService` (ensayo publicado en `App-backup-final`). El control utiliza una canalizacion local accesible solo a la misma identidad Windows; no expone una ruta HTTP. El ciclo:

1. Identifica la instancia por su puerto y comprueba la instancia SQL local, base solicitada y carpeta efectiva de evidencias.
2. Obtiene en memoria binario, directorio y variables de configuracion necesarias para reiniciar. No escribe secretos en argumentos, logs ni archivos de estado.
3. Solicita cierre ordenado, espera al proceso y verifica que el puerto queda libre. No fuerza procesos.
4. Respalda SQL y evidencias sin escrituras de la aplicacion.
5. Intenta reiniciar la misma publicacion incluso si falla el respaldo y comprueba su puerto. Los logs de arranque quedan en `ApplicationLogs` dentro del destino SQL.

La aplicacion debe ejecutarse con la misma cuenta Windows y nivel de elevacion que la tarea, como proceso independiente `dotnet.exe` o `BitacoraEvidencias.Web.exe`. Este mecanismo no administra IIS ni servicios Windows. Si la aplicacion ya estaba detenida, respalda sin iniciarla. Durante la ventana no iniciar otra instancia, importar datos, ni modificar directamente SQL o archivos. Si se interrumpe Windows o el proceso de la tarea, la configuracion en memoria desaparece: recuperar con el lanzador habitual y los secretos bajo custodia.

Preparar una tarea nueva, **deshabilitada**, sin tocar la tarea SQLite:

```powershell
.\scripts\backup\Register-BitacoraSqlBackupTask.ps1 `
  -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlBackup.credential.xml `
  -SqlBackupDirectory C:\BitacoraEvidencias\SqlBackupStaging `
  -BackupRoot D:\BackupsBitacora
```

Agregar `-TrustLocalSqlCertificate` solo si corresponde a la configuracion local autorizada. La tarea `BitacoraEvidencias-SqlBackup-Diario` se registra a las 14:00, zona de Mexico. Usa la cuenta actual con sesion Windows iniciada y elevacion limitada; **no se ejecutara si esa cuenta cerro sesion**. Ignora ejecuciones simultaneas y no recupera horarios perdidos fuera de la ventana. No tiene limite de duracion que pueda interrumpir el respaldo mientras la aplicacion esta detenida.

Antes de activarla: migrar produccion a SQL, publicar el control local, probar el ciclo con la configuracion productiva y aprobar la restauracion. Solo durante ese cambio se habilitara la tarea SQL y se deshabilitara la tarea SQLite. El registro y la activacion no forman parte del ensayo actual; no ejecutar ambos respaldos sobre produccion simultaneamente.

## Ensayo y ejecucion manual

Con la instancia de pruebas iniciada en 5068 y el archivo de credencial preparado:

```powershell
.\scripts\backup\Invoke-BitacoraScheduledSqlBackup.ps1 `
  -DatabaseName BitacoraEvidencias_Pruebas `
  -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlBackup.credential.xml `
  -SqlBackupDirectory C:\BitacoraEvidencias\SqlBackupStaging
```

El ensayo usa `C:\BitacoraEvidencias_Pruebas\Input\Evidencias`; nunca las evidencias productivas. Para un respaldo completo directo, detener primero la aplicacion y usar `Backup-Bitacora.ps1 -DatabaseName BitacoraEvidencias_Pruebas -ApplicationStopped` con los mismos parametros de credencial/carpeta SQL. `-DatabaseOnly` permite respaldar solo SQL con la aplicacion funcionando: no contiene evidencias y no sustituye un respaldo completo.

Comprobaciones sin conexion SQL: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validation/Test-SqlBackup.ps1`.

## Restauracion

`VERIFYONLY` y SHA256 no sustituyen una restauracion real. Restaurar el `.bak` con SQL Server en una **base nueva**, usando `RESTORE FILELISTONLY` y `WITH MOVE` hacia archivos nuevos, nunca `WITH REPLACE` sobre produccion. Ejecutar `DBCC CHECKDB`, comprobar tablas/conteos, extraer evidencias a otra carpeta y arrancar una publicacion de ensayo con la misma llave y configuracion de recuperacion. Validar acceso, lectura de datos cifrados y fotografias antes de considerar recuperable el conjunto. Conservar la instalacion y el respaldo SQLite anteriores durante el cambio.

Referencias: [BACKUP de SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/statements/backup-transact-sql), [respaldos COPY_ONLY](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/copy-only-backups-sql-server) y [dispositivos y permisos de respaldo](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/backup-devices-sql-server).

## Resultado del ensayo del 19/10/2026

- ZIP completo de pruebas creado y verificado: 3,656 archivos de evidencias (originales y miniaturas) mas BAK y manifiesto. Duracion aproximada: seis minutos en este equipo.
- BAK extraido de ese ZIP y restaurado en una base temporal nueva: `DBCC CHECKDB` sin incidencias. Conteos identicos: 6 usuarios, 824 oficios, 893 casos, 1,828 evidencias y 1,943 entradas de auditoria. Estos valores corresponden a la copia de pruebas, que ya tiene escrituras del ensayo.
- Base temporal eliminada al terminar. Produccion y base de ensayo conservadas.
- Cierre ordenado y reinicio comprobados en una instancia aislada; un fallo provocado de respaldo devolvio error y aun asi recupero el login HTTP 200. Carpeta de evidencias incorrecta rechazada antes de detener la instancia.
- 39 comprobaciones PowerShell y seis pruebas del estado de reinicio aprobadas. Tras adaptar por separado las pruebas SQLite/almacenamiento, la suite general Debug aprobo 132 casos sin fallos ni omitidas; ver [TESTING.md](TESTING.md) para alcance.
- No se registro ni activo la tarea SQL, no se modifico la tarea SQLite y no se migro produccion. Falta preparar localmente la credencial SQL protegida, ensayar con ella y activar durante el cambio productivo.
