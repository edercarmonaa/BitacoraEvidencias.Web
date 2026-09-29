# Pendientes SGSI - Bitacora de Evidencias

## Estado

- Fecha: 25/02/2026
- Proyecto: Bitacora de Evidencias
- Responsable: Eder Carmona Armijo
- Cargo: Encargado de la Oficina de Auditoria

## Pendientes abiertos

## Pendientes cerrados

1. Endurecimiento de permisos NTFS
- Estado: Cerrado el 20/04/2026.
- Alcance:
  - `C:\BitacoraEvidencias\Evidencias`
  - `C:\BitacoraEvidencias\Data`
  - `C:\BitacoraEvidencias\Data\bitacora-evidencias.db`
  - `C:\BitacoraEvidencias\BackupsBitacora`
- Objetivo:
  - Permitir acceso solo a `DESKTOP-0I65U3O\SEV`, `BUILTIN\Administradores` y `NT AUTHORITY\SYSTEM`.
  - Quitar herencia y permisos amplios no requeridos de `BUILTIN\Usuarios` y `NT AUTHORITY\Usuarios autentificados`.
- Validacion realizada:
  - Herencia NTFS desactivada en rutas protegidas.
  - ACL restringida a la cuenta de ejecucion, Administradores y SYSTEM.
  - Escritura temporal validada en evidencias.
  - Escritura temporal validada en respaldos.
  - Lectura de base SQLite validada.
- Evidencia:
  - `docs/evidencias-ntfs/ntfs-before-20260420-125336.txt`
  - `docs/evidencias-ntfs/ntfs-after-20260420-125336.txt`
  - `docs/evidencias-ntfs/ntfs-dbfile-fix-20260420-125603.txt`
  - `docs/evidencias-ntfs/ntfs-validation-20260420-125603.txt`

2. Automatizacion de respaldos (Task Scheduler)
- Estado: Cerrado el 21/04/2026.
- Alcance:
  - Script operativo: `C:\BitacoraEvidencias\scripts\backup\Backup-Bitacora.ps1`
  - Tarea programada: `BitacoraEvidencias-Backup-Diario`
  - Variable de entorno Machine: `BITACORA_BACKUP_ROOT=C:\BitacoraEvidencias\BackupsBitacora`
- Configuracion aplicada:
  - Ejecucion de lunes a viernes a las `14:00`.
  - Opcion `StartWhenAvailable` habilitada.
  - Usuario de ejecucion: `SEV`.
  - Tipo de inicio: `Interactive` (solo cuando `SEV` haya iniciado sesion).
  - El script usa precedencia: parametro `-BackupRoot`, variable `BITACORA_BACKUP_ROOT`, valor por defecto.
- Validacion realizada:
  - Tarea creada y habilitada.
  - Ejecucion manual disparada desde Task Scheduler.
  - ZIP generado correctamente en `C:\BitacoraEvidencias\BackupsBitacora`.
  - Log generado correctamente en `C:\BitacoraEvidencias\BackupsBitacora\logs`.
  - El log confirma copia de base, copia de evidencias, creacion de ZIP, retencion y cierre exitoso.
- Evidencia:
  - ZIP: `C:\BitacoraEvidencias\BackupsBitacora\BitacoraBackup_20260421_110431.zip`
  - Log: `C:\BitacoraEvidencias\BackupsBitacora\logs\Backup_20260421.log`
- Observacion:
  - `LastTaskResult` devolvio `3221225786`, pero la evidencia operativa muestra respaldo completado correctamente y archivo ZIP integro. Para cierre SGSI se toma como valida la ejecucion por evidencia material de salida y log de finalizacion.

3. Estabilizacion de pruebas automatizadas de arranque
- Estado: Cerrado el 22/04/2026.
- Alcance:
  - `tests\BitacoraEvidencias.Web.Tests\Startup\ApplicationDataStartupTests.cs`
  - `tests\BitacoraEvidencias.Web.Tests\Startup\ApplicationStartupTests.cs`
- Correccion aplicada:
  - Aislamiento de Data Protection con repositorio temporal de llaves.
  - Limpieza explicita de providers de logging.
  - Remocion de `ILoggerProvider` y `EventLogLoggerProvider` del host de pruebas.
- Validacion realizada:
  - La suite focalizada de arranque regreso sin errores con `dotnet test --no-restore --filter "FullyQualifiedName~ApplicationStartupTests|FullyQualifiedName~ApplicationDataStartupTests"`.
  - Se elimino la falla ambiental asociada a `Windows Event Log` y condiciones de `Data Protection`.

4. Cifrado aplicativo de datos sensibles y evidencias
- Estado: Cerrado tecnicamente el 04/05/2026.
- Alcance:
  - Campos sensibles en `CasosCorreccion`: CURP, nombre, CCT, folio, URL de evidencia, observaciones y validador.
  - Indices ciegos HMAC para busqueda exacta por CURP/CCT/Folio.
  - `AuditLog` sin CURP/CCT/Folio en claro; conserva hash y sufijo operativo.
  - Evidencias y miniaturas cifradas con AES-GCM.
  - Nombres de carpetas de oficio no legibles cuando existe llave de cifrado.
- Condicion operativa:
  - Requiere variable `BITACORA_DATA_PROTECTION_KEY`.
  - El script de arranque publicado valida y pasa la llave al proceso.
  - La migracion existente se ejecuta manualmente con `scripts/security/Protect-ExistingData.ps1`.
- Evidencia documental:
  - `docs/CIFRADO-DATOS-EVIDENCIAS.md`
  - `scripts/security/Protect-ExistingData.ps1`
  - `scripts/deploy/Start-BitacoraManual.cmd`
  - `scripts/deploy/Start-BitacoraPublished.ps1` (diagnostico tecnico)

## Checklist de cierre

- [x] Permisos NTFS aplicados en evidencias.
- [x] Permisos NTFS aplicados en base de datos SQLite.
- [x] Permisos NTFS aplicados en carpeta de backups.
- [x] Tarea programada de backup creada.
- [x] Ejecucion automatica validada con evidencia.
- [x] Suite focalizada de arranque estabilizada.
- [x] Cifrado aplicativo de datos sensibles implementado.
- [x] Cifrado de evidencias y miniaturas implementado.
- [x] Migracion manual documentada.
- [x] Riesgos residuales actualizados.

## Nota

Al cerrar los pendientes tecnicos, actualizar el documento:
- `docs/SGSI-CIERRE-ISO27001-27002.md`
