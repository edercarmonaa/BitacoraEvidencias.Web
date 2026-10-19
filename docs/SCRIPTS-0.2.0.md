# Scripts alineados a 0.2.0

Revision 19/10/2026. La revision inicial fue anterior al [corte productivo en curso](CORTE-PRODUCTIVO-0.2.0.md); consultar ese registro para importacion, tareas y arranque actuales.

Se ajusto la exclusion de Storage para limitarla a la carpeta de datos de la raiz: tambien estaba ocultando scripts/storage en Windows. El helper de almacenamiento queda ahora versionado y bloqueado en la rama SQL; las referencias congeladas v0.1.0 no se modifican.

## Por proveedor y funcion

| Grupo | Scripts / criterio |
| --- | --- |
| Migracion SQL | Invoke-BitacoraSqlMigration.ps1 y Expand-BitacoraRehearsalBackup.ps1: copias de origen, preflight/import/verify/upgrade/profile; no repetir import sobre destino ocupado. |
| Ensayo SQL | Publish-BitacoraSqlServer.ps1 / Start-BitacoraSqlServer.ps1: carpetas nuevas, base de pruebas y puerto 5068. |
| Publicacion SQL independiente | Publish-BitacoraSqlRelease.ps1: release nuevo en deploy-sql, sin reemplazar current SQLite, activar, iniciar ni limpiar releases anteriores. NoRestore opcional despues de restaurar dependencias. |
| Arranque SQL productivo | Start-BitacoraSqlProduction.ps1: ApplicationFolder obligatorio, base BitacoraEvidencias, TLS, credencial local/DPAPI y secretos externos. Puerto 5067 ocupado bloquea arranque. ValidateOnly no conecta a SQL ni valida el certificado; solo comprueba binarios/rutas/variables/puerto. |
| Arranque SQL instalado | Start-BitacoraSqlInstalled.ps1: lee manifiesto externo y configuracion SecureString DPAPI con la misma cuenta Windows, carga temporalmente llave/certificado e invoca el lanzador productivo. Restaura el entorno al terminar; no genera secretos ni modifica SQLite. ValidateOnly conserva el alcance limitado del lanzador. |
| Backup SQL | Backup-Bitacora.ps1, SqlBackupSupport, Invoke-BitacoraScheduledSqlBackup, Initialize-BitacoraSqlBackupDirectory, Save-BitacoraSqlBackupCredential y Register-BitacoraSqlBackupTask. Ver [backup](BACKUP-OPERACION.md). |
| Auditoria/monitor SQL | Archive-AuditLog.ps1, Monitor-Bitacora.ps1, SqlOperationsSupport y Register-BitacoraSqlOperationsTasks. Ver [operaciones](AUDITORIA-MONITOREO-0.2.0.md). |
| SQLite conservado | Backup-BitacoraSqlite, Archive-AuditLogSqlite y Monitor-BitacoraSqlite, sin cambios en su contenido original. |
| SQLite desde codigo | Initialize-BitacoraDatabase, Test-BitacoraLegacySchema, Stamp-BitacoraLegacyBaseline, Publish-Bitacora, Start-BitacoraWithHttps, Protect-ExistingData, Migrate-LegacyStorage y Run-StartupValidation: bloqueados en la rama SQL antes de escribir/borrar archivos, crear carpetas, cambiar entorno o arrancar. Usar los originales de v0.1.0 en worktree separado. |
| Arranque SQLite instalado | Start-BitacoraManual.cmd y Start-BitacoraPublished.ps1 conservan destino/puerto. El tecnico comprueba dependencias/proveedor: reconoce tambien el binario anterior a numeracion, AssemblyVersion 1.0.0.0. Se retiro taskkill por nombre del CMD para no cerrar otra version. |
| Certificados | New-LanCertificate: no sobrescribe material existente, no imprime contrasena, genera secreto aleatorio criptografico y lo conserva en credential.xml DPAPI de la misma cuenta/equipo; tambien configura entorno del proceso, o User si se solicita explicitamente. No se genero ni renovo un certificado durante esta revision. |
| Frontend | Setup-FrontendAssets mantiene HTMX 1.9.12 y fija Tailwind 4.2.1, igual que el CSS versionado, en vez de descargar latest. Build-Tailwind usa @config del archivo de entrada v4 y devuelve fallo si el compilador falla. No se descargaron ni sustituyeron assets en esta revision. |
| Smokes | Run-Smoke / Run-Smoke-Curl / Smoke.Common: casos con campos obligatorios y CURP sintetica valida por formato, dia de Mexico, carpetas HMAC e imagen valida. Ver requisitos siguientes. |
| Validaciones | Test-ScriptAlignment, Test-SqlServerConnectionScripts, Test-SqlBackup, Test-SqlOperations y Test-FormFeedback.cjs. Los resultados de cada tipo se documentan por separado. |

## Smokes con escrituras

Usar solo una copia local, fuera del puerto 5067 y de las evidencias productivas. Es obligatorio AllowWriteTests: crean/desactivan usuarios, prueban bloqueo/roles y crean/borran expedientes. Suministrar BITACORA_SMOKE_ADMIN_PASSWORD y la misma llave del ensayo por el mecanismo local seguro.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/smoke/Run-Smoke.ps1 -BaseUrl https://127.0.0.1:5068 -StorageRoot C:\BitacoraEvidencias_Pruebas\Input\Evidencias -AllowWriteTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/smoke/Run-Smoke-Curl.ps1 -BaseUrl https://127.0.0.1:5068 -StorageRoot C:\BitacoraEvidencias_Pruebas\Input\Evidencias -AllowWriteTests
```

HTTPS valida confianza por defecto. TrustLocalHttpsCertificate es una excepcion explicita solo para laboratorio y distinta de TrustLocalSqlCertificate. Los requests tienen limites de tiempo. La variante curl guarda cuerpos/cookies/respuestas temporales con ACL privada y no pasa contrasenas de formularios como argumentos; limpia su carpeta propia. Reportes privados en tmp/smoke-reports. Cualquier FAIL devuelve error; el timeout de sesion de 30 minutos queda SKIP y requiere prueba manual.

## Release y configuracion sin activacion

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Publish-BitacoraSqlRelease.ps1
# Sustituir la ruta por el release realmente creado. No inicia ni consulta SQL.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlProduction.ps1 -ApplicationFolder C:\BitacoraEvidencias\deploy-sql\releases\RELEASE_CREADO -ValidateOnly
```

El arranque real sin ValidateOnly se reserva para el cambio aprobado. La aplicacion exige esquema completo y usuarios migrados; el lanzador no prepara/resetea bases. Credencial opcional mediante BITACORA_APPLICATION_SQL_CREDENTIAL_FILE, creada con Save-BitacoraSqlBackupCredential usando la cuenta SQL de operacion y un archivo separado. Mantener llave y certificado existentes.

## Evidencia

70 comprobaciones locales de compatibilidad (con binarios compilados), diez escenarios de wrappers SQL y suites de backup/operaciones aprobados. La publicacion inicial y ValidateOnly se comprobaron sin iniciar produccion; el posterior corte, arranque instalado DPAPI y ciclo real de respaldo/reinicio figuran en el registro productivo. Binario SQLite instalado reconocido por version/dependencias y conservado.

Ambos smokes ejecutados en base SQL nueva con datos sinteticos: **47 PASS, 0 FAIL, 1 SKIP cada uno**. Proceso aislado cerrado y base temporal propia eliminada. No se modificaron registros de las bases productiva ni de ensayo existentes. Estos resultados no sustituyen aceptacion visual ni ensayo final con cuenta/permisos SQL productivos.
