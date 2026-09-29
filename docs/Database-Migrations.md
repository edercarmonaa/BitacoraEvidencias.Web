# Migraciones de Base de Datos

## Estado actual

La aplicacion usa migraciones de EF Core almacenadas en `Data/Migrations`.

Al preparar datos con startup explicito:
- bases nuevas o reiniciadas: se aplica `Database.Migrate()`
- bases SQLite legacy sin `__EFMigrationsHistory`: fallan por defecto
- solo se permite compatibilidad legacy temporal con `Startup:AllowLegacySchemaWithoutMigrations=true`

## Flujo recomendado

### 0. Prechequeo de base legacy

Antes de desplegar una version nueva sobre una base existente, ejecuta:

```powershell
.\scripts\db\Test-BitacoraLegacySchema.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\BitacoraEvidencias.Web\Data\bitacora-evidencias.db" `
  -EnvironmentName "Production"
```

Resultado esperado:
- si la base ya tiene `__EFMigrationsHistory`, el comando termina correctamente
- si la base es legacy, el comando falla con mensaje explicito

### 1. Entorno nuevo

Usa el script de inicializacion:

```powershell
.\scripts\db\Initialize-BitacoraDatabase.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\BitacoraEvidencias.Web\Data\bitacora-evidencias.db" `
  -StorageRoot "C:\BitacoraEvidencias\Evidencias" `
  -EnvironmentName "Production"
```

Eso prepara storage, aplica migraciones y crea el admin bootstrap si no existen usuarios.

### 2. Actualizacion normal

Antes de iniciar una nueva version, ejecuta el mismo flujo de preparacion explicita:

```powershell
.\scripts\db\Initialize-BitacoraDatabase.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\BitacoraEvidencias.Web\Data\bitacora-evidencias.db" `
  -StorageRoot "C:\BitacoraEvidencias\Evidencias" `
  -EnvironmentName "Production"
```

### 3. Base legacy sin historial de migraciones

Si la base fue creada antes de introducir `Data/Migrations`, la aplicacion ahora la bloquea por defecto.

El flujo recomendado ya no es dejar el flag legacy encendido, sino:
- detectar la base legacy
- respaldarla
- registrar el baseline EF Core
- ejecutar la inicializacion normal sin `AllowLegacySchemaWithoutMigrations`

## Plan de migracion controlada para una base legacy existente

### Ensayo previo

1. Restaurar una copia de la base legacy en un entorno de ensayo.
2. Ejecutar:

```powershell
.\scripts\db\Test-BitacoraLegacySchema.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\Data\bitacora-evidencias.db" `
  -EnvironmentName "Production"
```

3. Confirmar que el resultado indique base legacy sin `__EFMigrationsHistory`.
4. Ejecutar el stamp controlado del baseline:

```powershell
.\scripts\db\Stamp-BitacoraLegacyBaseline.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\Data\bitacora-evidencias.db" `
  -EnvironmentName "Production"
```

5. Ejecutar la inicializacion normal, ya sin flag legacy:

```powershell
.\scripts\db\Initialize-BitacoraDatabase.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\Data\bitacora-evidencias.db" `
  -StorageRoot "C:\BitacoraEvidencias\Evidencias" `
  -EnvironmentName "Production"
```

6. Verificar funcionalmente: login admin, busqueda, detalle de casos, evidencias y auditoria.

### Produccion

1. Detener la aplicacion.
2. Ejecutar respaldo completo de base y evidencias.
3. Ejecutar el prechequeo:

```powershell
.\scripts\db\Test-BitacoraLegacySchema.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\Data\bitacora-evidencias.db" `
  -EnvironmentName "Production"
```

4. Si la base es legacy, ejecutar:

```powershell
.\scripts\db\Stamp-BitacoraLegacyBaseline.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\Data\bitacora-evidencias.db" `
  -EnvironmentName "Production"
```

5. Ejecutar la preparacion normal:

```powershell
.\scripts\db\Initialize-BitacoraDatabase.ps1 `
  -DataDbPath "C:\BitacoraEvidencias\Data\bitacora-evidencias.db" `
  -StorageRoot "C:\BitacoraEvidencias\Evidencias" `
  -EnvironmentName "Production"
```

6. Iniciar la aplicacion.
7. Ejecutar smoke tests.

### Rollback

Si algo falla despues del stamp del baseline o de la inicializacion:

1. detener la aplicacion
2. restaurar el archivo `.db` desde el backup creado por `Stamp-BitacoraLegacyBaseline.ps1`
3. restaurar tambien `.db-wal` y `.db-shm` si existian
4. volver a la version anterior de la aplicacion
5. arrancar y validar

## Agregar una nueva migracion

Cuando cambie el modelo EF Core:

```powershell
dotnet ef migrations add NombreDeLaMigracion --output-dir Data\Migrations
```

Luego valida:

```powershell
dotnet test BitacoraEvidencias.Web.sln
```

## Despliegue sugerido

1. respaldar base y evidencias
2. publicar release
3. ejecutar `Initialize-BitacoraDatabase.ps1`
4. iniciar la version publicada
5. correr smoke tests

Scripts relacionados:
- `scripts/db/Test-BitacoraLegacySchema.ps1`
- `scripts/db/Stamp-BitacoraLegacyBaseline.ps1`
- `scripts/db/Initialize-BitacoraDatabase.ps1`
- `scripts/security/Protect-ExistingData.ps1`
- `scripts/deploy/Publish-Bitacora.ps1`
- `scripts/smoke/Run-Smoke.ps1`
- `scripts/validation/Run-StartupValidation.ps1`

## Migracion de cifrado aplicativo

Desde el 04/05/2026 existen columnas de soporte para cifrado y busqueda exacta:

- `CasosCorreccion.CurpHash`
- `CasosCorreccion.CctHash`
- `CasosCorreccion.FolioHash`
- `AuditLogs.CurpHash`, `CurpSuffix`
- `AuditLogs.CctHash`, `CctSuffix`
- `AuditLogs.FolioCertificadoHash`, `FolioCertificadoSuffix`

La migracion de datos existentes no se ejecuta automaticamente. Se ejecuta manualmente despues de respaldo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data" -StorageRoot "C:\BitacoraEvidencias\Evidencias"
```

Requiere `BITACORA_DATA_PROTECTION_KEY` configurada. Si se desea respaldo integrado antes de cifrar, agregar `-CreateBackup $true`.
