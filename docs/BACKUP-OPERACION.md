# Respaldo a Disco Externo

Script de respaldo para:
- `Data\bitacora-evidencias.db` (+ `-wal`, `-shm` si existen)
- `C:\BitacoraEvidencias\Evidencias`
- compresion en ZIP con timestamp
- retencion automatica
- log por ejecucion

## Ruta y retencion configuradas

- Destino: `D:\BackupsBitacora`
- Retencion: `30` dias

## Nota sobre cifrado

Desde el 04/05/2026 el sistema puede operar con cifrado aplicativo de datos sensibles y evidencias mediante `BITACORA_DATA_PROTECTION_KEY`.

Los respaldos contienen los datos y archivos en el estado fisico en que se encuentren al momento de respaldar:
- si la migracion de cifrado ya fue ejecutada, la base contiene campos `enc:v1:...` y las evidencias tienen prefijo `BITACORA-ENC-V1`;
- si se respalda antes de la migracion, el respaldo puede contener informacion en claro.

La llave `BITACORA_DATA_PROTECTION_KEY` no debe guardarse dentro del ZIP de respaldo. Debe resguardarse por separado, porque sin ella no se podran restaurar datos ni evidencias cifradas.

## Ejecucion manual

Desde la raiz del proyecto:

```powershell
.\scripts\backup\Backup-Bitacora.ps1
```

Opcional (incluye BD de desarrollo):

```powershell
.\scripts\backup\Backup-Bitacora.ps1 -IncludeDevDatabase
```

## Resultado esperado

1. ZIP creado en `D:\BackupsBitacora\BitacoraBackup_YYYYMMDD_HHMMSS.zip`
2. Log en `D:\BackupsBitacora\logs\Backup_YYYYMMDD.log`
3. Limpieza automatica de ZIP antiguos (>30 dias)

## Respaldo antes de migrar cifrado

Antes de ejecutar:

```powershell
.\scripts\security\Protect-ExistingData.ps1
```

debe existir respaldo reciente generado por publish o por este script de backup.

## Programar en Task Scheduler (recomendado)

1. Crear tarea: ejecutar diario (por ejemplo 22:30).
2. Programa/script:
   - `powershell.exe`
3. Argumentos:
   - `-NoProfile -ExecutionPolicy Bypass -File "C:\BitacoraEvidencias\BitacoraEvidencias.Web\scripts\backup\Backup-Bitacora.ps1"`
4. Ejecutar con una cuenta que tenga acceso de lectura a:
   - `C:\BitacoraEvidencias\BitacoraEvidencias.Web\Data`
   - `C:\BitacoraEvidencias\Evidencias`
5. Ejecutar la tarea una vez manualmente y validar ZIP + log.
