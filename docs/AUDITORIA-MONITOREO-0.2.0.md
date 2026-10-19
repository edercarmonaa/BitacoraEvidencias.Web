# Auditoria y monitoreo SQL Server 0.2.0

Instancia `.\Auditoria`, base productiva `BitacoraEvidencias`, ensayo `BitacoraEvidencias_Pruebas`. Los originales se conservan como `Archive-AuditLogSqlite.ps1` y `Monitor-BitacoraSqlite.ps1`, tambien accesibles mediante `-Provider Sqlite`. `v0.1.0` y `archive/sqlite-0.1` permanecen intactos.

## Politica confirmada

Monitoreo **09:15**, archivado **09:20**, respaldo **14:00**, hora de Mexico. Mantener **30 dias en SQL** y el resto en archivos historicos, sin eliminacion automatica de historicos. Las tareas SQL se preparan deshabilitadas; las SQLite siguen funcionando hasta migrar produccion. Las nuevas tareas requieren sesion Windows abierta (puede estar bloqueada).

## Credencial

```powershell
.\scripts\backup\Save-BitacoraSqlBackupCredential.ps1 -CredentialPath C:\BitacoraEvidencias\Secrets\SqlOperations.credential.xml
```

DPAPI protege usuario/contrasena SQL para la misma cuenta Windows y equipo. Usar `-SqlCredentialFile` o `BITACORA_OPERATIONS_SQL_CREDENTIAL_FILE`. Sin archivo solicita credenciales manualmente; `-NonInteractive` falla si faltan. TLS permanece activo. `-TrustLocalSqlCertificate` es una excepcion explicita para SQL local sin cadena de confianza; no modifica HTTPS del monitor.

Monitoreo requiere lectura de AuditLogs, historial EF y metadatos. Archivado requiere ademas DELETE en AuditLogs y bloqueo SQL de aplicacion. No se otorgan permisos automaticamente. No necesita llave de cifrado: exporta valores fisicos sin descifrarlos.

## Archivado

```powershell
.\scripts\audit\Archive-AuditLog.ps1 -DatabaseName BitacoraEvidencias_Pruebas -Mode Preview -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlOperations.credential.xml
.\scripts\audit\Archive-AuditLog.ps1 -DatabaseName BitacoraEvidencias_Pruebas -Mode Export -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlOperations.credential.xml
# Archive solo despues de revisar el ensayo y disponer de respaldo completo.
.\scripts\audit\Archive-AuditLog.ps1 -DatabaseName BitacoraEvidencias_Pruebas -Mode Archive -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlOperations.credential.xml
```

Modo predeterminado **Preview**: cuenta sin retirar filas. Export crea archivos sin borrar. Archive aplica los 30 dias. Destino productivo `C:\BitacoraEvidencias\AuditArchive\SqlServer\BitacoraEvidencias`; ensayo `C:\BitacoraEvidencias_Pruebas\AuditArchive\SqlServer\BitacoraEvidencias_Pruebas`. ArchiveRoot cambia la raiz manteniendo separacion por base. Las carpetas SQL nuevas limitan permisos a cuenta actual, SYSTEM y administradores.

Procesa lotes de 200 filas (configurable 1..500), corte UTC fijo y orden por Id. Exporta todas las columnas, nulos, hashes, sufijos y cifrado a JSONL con nombres unicos; manifiesto con columnas/tipos/conteo/SHA256. Relee y compara todos los valores, publica y verifica SHA256 antes de retirar solo los identificadores del lote dentro de la misma transaccion de lectura/borrado. Si DELETE falla, revierte ese lote; los anteriores confirmados permanecen archivados. No sobrescribe archivos diarios anteriores ni requiere la herramienta SQLite.

Una interrupcion puede dejar una copia cuyo lote no se retiro, o un manifiesto sin confirmacion despues del commit SQL. Conservarlo y conciliar por Id/base; no eliminar duplicados automaticamente. Los `.partial` no autorizan retirar filas. Bloqueos de archivo/SQL impiden archivados simultaneos; el respaldo completo comparte el recurso y rechaza solapamientos. No cambia el aislamiento global del servidor.

Estados y logs contienen conteos/metadatos, sin registros personales ni mensajes del proveedor. `latest-archive-status.json` conserva el ultimo intento de Archive para que Preview/Export no lo oculten. Errores devuelven fallo a Task Scheduler.

## Monitoreo

```powershell
.\scripts\monitor\Monitor-Bitacora.ps1 -DatabaseName BitacoraEvidencias_Pruebas -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlOperations.credential.xml
```

Comprueba HTTP, SQL/migracion inicial, respaldo completo reciente de esa base, disco C/destino de respaldo, evidencias, tamanos SQL de datos/log y conteos agregados LOGIN_FAIL/LOCKOUT/DELETE/DELETE_EVIDENCE. No consulta ni mide SQLite. URL sin credenciales, parametros ni fragmentos.

El ZIP necesita manifiesto valido, BAK, evidencias e historicos. Rechaza SQLite, otra base, parciales y DatabaseOnly. Comprueba entradas/tamanos; **no recalcula hashes ni sustituye VERIFYONLY/restauracion**. Un respaldo viejo sin indicador de historicos requiere generar otro.

Umbrales: 25 GB libres, 50 GB evidencias, 5 GB datos SQL, respaldo de hasta 26 horas (margen de dos horas sobre el ciclo diario). Log SQL informado separado; estos umbrales no son limites de licencia. Eventos positivos requieren revision del operador y no son por si solos fallo tecnico. Carpeta ausente, SQL/URL inaccesibles o ZIP invalido generan ALERT y salida de fallo. Reportes separados en `Monitoreo\SqlServer\<base>`: latest-status, latest-run, historial status y logs.

## Preparar tareas

```powershell
.\scripts\operations\Register-BitacoraSqlOperationsTasks.ps1 -SqlCredentialFile C:\BitacoraEvidencias\Secrets\SqlOperations.credential.xml -BackupRoot D:\BackupsBitacora
```

Crea `BitacoraEvidencias-SqlMonitor-Diario` (09:15) y `BitacoraEvidencias-SqlAuditArchive-Diario` (09:20, Mode Archive, 30 dias), deshabilitadas, con cuenta actual/sesion abierta. No sobrescribe ni activa tareas; ignora solapamientos y horarios perdidos. Conservar disponibles las rutas de scripts. Agregar la excepcion del certificado SQL solo si corresponde.

Durante el cambio: respaldo con historicos anteriores, permisos/credenciales y ensayo aprobados, deshabilitar tareas SQLite y habilitar SQL. Conservar archivos SQLite anteriores. Si cambia ArchiveRoot, pasar la misma raiz como `-AuditArchiveRoot` al respaldo completo y ejecutor programado.

## Verificacion 19/10/2026

`scripts/validation/Test-SqlOperations.ps1` realiza comprobaciones locales; `-Integration` crea y elimina una base temporal propia con datos sinteticos. Probados Preview, Export, retirada transaccional, reversion de DELETE provocado, ejecucion repetida y preservacion de datos protegidos/columnas auxiliares. Ejecutar tambien `scripts/validation/Test-SqlBackup.ps1`.

31 comprobaciones de operaciones y 39 de respaldo aprobadas. Definiciones de tareas comprobadas con simulaciones locales, sin registrarlas. Monitor probado con Login real aislado y respaldo completo; se redujo el umbral de disco solo en esa prueba, pues C tiene aproximadamente 23 GB libres y el umbral normal de 25 GB genera ALERT.

Exportados 1,900 registros reales de ensayo en diez lotes sin retirar ninguno. Respaldo completo de ensayo con 3,656 archivos de evidencias y 20 historicos/manifiestos, comparados por SHA256. Tareas productivas y datos de produccion intactos.

Referencia: [transacciones SQL Server](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide) y [UPDLOCK](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table).
