# Operacion Diaria - Bitacora de Evidencias

## 1) Datos base

- URL oficial LAN: `https://<SERVER_IP>:5067`
- Modo: HTTPS estricto (sin HTTP)
- Evidencias: `C:\BitacoraEvidencias\Evidencias`
- Base principal produccion: `C:\BitacoraEvidencias\Data\bitacora-evidencias.db`
- Backups: `C:\BitacoraEvidencias\BackupsBitacora`
- Retencion de backup: `30` dias
- Monitoreo: `C:\BitacoraEvidencias\Monitoreo`
- Archivo historico de auditoria: `C:\BitacoraEvidencias\AuditArchive`
- Llave de cifrado: variable `BITACORA_DATA_PROTECTION_KEY` a nivel `Machine`

## 2) Arranque del sistema

Documento especifico del cierre de arranque manual:
- `docs/ARRANQUE-MANUAL.md`

Preparacion inicial obligatoria si la BD no existe o no esta lista:

```powershell
.\scripts\db\Initialize-BitacoraDatabase.ps1
```

1. Abrir PowerShell en:
- `C:\BitacoraEvidencias\BitacoraEvidencias.Web`

2. Publicar release (recomendado para operacion):

```powershell
.\scripts\deploy\Publish-Bitacora.ps1
```

Por defecto el publish ejecuta respaldo previo. Para controlarlo explicitamente:

```powershell
.\scripts\deploy\Publish-Bitacora.ps1 -CreateBackup true
.\scripts\deploy\Publish-Bitacora.ps1 -CreateBackup false
```

Forma directa equivalente para omitir respaldo:

```powershell
.\scripts\deploy\Publish-Bitacora.ps1 -NoBackup
```

Tambien se puede indicar destino y retencion del respaldo:

```powershell
.\scripts\deploy\Publish-Bitacora.ps1 -CreateBackup true -BackupRoot "C:\BitacoraEvidencias\BackupsBitacora" -BackupRetentionDays 30
```

3. Iniciar manualmente la aplicacion publicada:

```bat
.\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>
```

Estado operativo acordado:
- No existe tarea programada de arranque automatico de la aplicacion.
- No existe archivo de inicio automatico en la carpeta Startup de Windows.
- El arranque se realiza manualmente desde `C:\BitacoraEvidencias\Iniciar Bitacora Manual.cmd` o desde el acceso del escritorio del usuario operativo.
- El launcher manual espera a que el puerto `5067` quede escuchando y abre `https://<SERVER_IP>:5067/Login`.
- Logs del arranque manual: `C:\BitacoraEvidencias\deploy\logs\manual-start.log`, `manual-app.out.log` y `manual-app.err.log`.

Alternativa tecnica para diagnostico:

```powershell
.\scripts\deploy\Start-BitacoraPublished.ps1 -ServerIp <SERVER_IP>
```

Alternativa modo proyecto fuente:

```powershell
.\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <SERVER_IP>
```

Ese script ahora valida antes de arrancar:
- Base SQLite existente
- Carpeta de evidencias existente
- Ausencia de carpeta legado `.\Storage` pendiente de migracion
- Llave `BITACORA_DATA_PROTECTION_KEY` disponible para descifrar datos y evidencias

4. Confirmar salida esperada en navegador o logs:
- `Servidor HTTPS estricto (sin HTTP)`
- `URL: https://<SERVER_IP>:5067`
- `Llave de cifrado: configurada (Machine)` o el origen correspondiente
- Si la preparacion creo una base nueva sin usuarios previos, se mostrara la contraseÃ±a temporal del usuario `admin`.

5. Validar acceso desde cliente LAN:
- Abrir `https://<SERVER_IP>:5067/Login`

Nota de seguridad para primer arranque:
- La app ya no prepara la base de forma implicita al iniciar.
- Si la base esta vacia, primero ejecuta `.\scripts\db\Initialize-BitacoraDatabase.ps1`.
- Durante esa preparacion se crea `admin` con contraseÃ±a temporal.
- La contraseÃ±a se debe cambiar en el primer acceso.
- Si quieres controlar esa contraseÃ±a inicial, define `Security__BootstrapAdminPassword` antes de iniciar la app.

Si existe carpeta legado `Storage` dentro del proyecto:

```powershell
.\scripts\storage\Migrate-LegacyStorage.ps1
```

La app ya no mueve esa carpeta automaticamente durante el arranque.

## 3) Checklist de apertura (inicio de jornada)

- [ ] La app responde por HTTPS.
- [ ] No hay advertencia de certificado en clientes.
- [ ] Login de `Admin` funciona.
- [ ] Se puede abrir al menos un caso y su evidencia.
- [ ] `/Admin/AuditLog` carga sin error.
- [ ] `C:\BitacoraEvidencias\Monitoreo\latest-status.json` fue revisado.
- [ ] El monitoreo no reporta `ALERT` no atendidos.

## 4) Operacion durante el dia

1. Roles:
- `Admin`: usuarios, auditoria, supervision.
- `Capturista`: altas/modificaciones/bajas y evidencias.

2. Regla operativa:
- No exponer ruta fisica de evidencias.
- Visualizar evidencias solo dentro del sistema.
- No rotar ni regenerar `BITACORA_DATA_PROTECTION_KEY` sin procedimiento formal de migracion.

3. Seguridad activa esperada:
- Bloqueo por 5 intentos fallidos.
- Timeout de sesion por inactividad (30 min).
- Cambio de clave obligatorio cuando aplique.
- Cifrado aplicativo activo de datos sensibles y evidencias cuando `BITACORA_DATA_PROTECTION_KEY` esta configurada.

4. Uso del modulo SQL administrativo:
- Se considera herramienta excepcional de administracion, depuracion y mantenimiento de datos.
- `SELECT` puede usarse sin respaldo previo cuando la consulta sea necesaria y especifica.
- `INSERT`, `UPDATE` y `DELETE` requieren respaldo reciente previo.
- Toda escritura debe documentar motivo, fecha y responsable en el control operativo correspondiente.
- Despues de una intervencion sensible, revisar `/Admin/AuditLog`.

## 5) Cierre diario (fin de jornada)

1. Verificar eventos criticos en bitacora:
- `LOGIN_FAIL`
- `LOCKOUT`
- `DELETE`
- `DELETE_EVIDENCE`

2. Verificar respaldo automatizado del dia:
- Tarea: `BitacoraEvidencias-Backup-Diario`
- Carpeta: `C:\BitacoraEvidencias\BackupsBitacora`
- Log: `C:\BitacoraEvidencias\BackupsBitacora\logs`

3. Verificar archivado automatico de auditoria:
- Tarea: `BitacoraEvidencias-AuditArchive-Diario`
- Carpeta: `C:\BitacoraEvidencias\AuditArchive`
- Log: `C:\BitacoraEvidencias\AuditArchive\logs`
- Regla: `AuditLog` mantiene `30` dias en SQLite; lo anterior se exporta a `.log` diario y se elimina de la base solo despues de exportacion correcta.

4. Ejecutar respaldo manual solo si hubo incidencia o si se requiere uno extraordinario:

```powershell
.\scripts\backup\Backup-Bitacora.ps1
```

5. Confirmar resultado:
- ZIP nuevo en `C:\BitacoraEvidencias\BackupsBitacora`
- Log actualizado en `C:\BitacoraEvidencias\BackupsBitacora\logs`

## 6) Checklist diario de cierre

- [ ] Se revisaron eventos criticos en `/Admin/AuditLog`.
- [ ] Se genero backup del dia.
- [ ] Existe ZIP con timestamp del dia.
- [ ] El log de backup no contiene `ERROR`.
- [ ] El archivado de auditoria no reporta `ERROR`.
- [ ] El monitoreo del dia siguiente a las `09:15` no refleja `ALERT` pendientes sin atender.

## 7) Prueba de restauracion (semanal o quincenal)

1. Restaurar ultimo ZIP en carpeta temporal:
- `C:\BitacoraEvidencias\RestoreTest\YYYYMMDD_HHMMSS`

2. Levantar app temporal con overrides:
- `ConnectionStrings__DefaultConnection` hacia la BD restaurada.
- `Storage__RootPath` hacia evidencias restauradas.

3. Validar:
- Login correcto.
- Consulta de oficios/casos.
- Visualizacion de evidencias.

4. Cerrar entorno temporal y arrancar produccion normal.

## 8) Revision de auditoria (minimo diario)

Filtros recomendados en `/Admin/AuditLog`:
- `Days=1`
- `EventType=LOCKOUT`
- `EventType=LOGIN_FAIL`
- `EventType=DELETE`
- `EventType=DELETE_EVIDENCE`

Campos a revisar:
- Usuario y rol
- IP
- Oficio/Consecutivo y sufijos de CURP/CCT/Folio cuando aplique
- Resultado `OK` (Si/No)

Politica de retencion:
- `AuditLog` en SQLite: `30` dias.
- Registros mas antiguos: se exportan automaticamente a `C:\BitacoraEvidencias\AuditArchive\AuditLog_YYYYMMDD.log`.
- Los `.log` historicos se conservan hasta instruccion o autorizacion administrativa.

## 9) Monitoreo basico automatizado

Tarea programada:
- `BitacoraEvidencias-Monitoreo-Diario`

Horario:
- Lunes a viernes a las `09:15`

Usuario:
- `SEV`

Salida:
- Log: `C:\BitacoraEvidencias\Monitoreo\logs\Monitor_YYYYMMDD.log`
- Estado actual: `C:\BitacoraEvidencias\Monitoreo\latest-status.json`
- Estado de ejecucion en curso: `C:\BitacoraEvidencias\Monitoreo\latest-run.json`
- Historial de estados: `C:\BitacoraEvidencias\Monitoreo\status`

Validaciones incluidas:
- Disponibilidad de `https://<SERVER_IP>:5067/Login`
- Existencia de ZIP reciente en las ultimas `24` horas
- Espacio libre en `C:` mayor a `25 GB`
- Tamano de `C:\BitacoraEvidencias\Evidencias` no mayor a `50 GB`
- Tamano de `C:\BitacoraEvidencias\Data\bitacora-evidencias.db` no mayor a `5 GB`
- Conteo en log de `LOGIN_FAIL`, `LOCKOUT`, `DELETE` y `DELETE_EVIDENCE` de las ultimas `24` horas

Interpretacion:
- `OK`: condicion dentro de parametros
- `ALERT`: requiere revision operativa

## 10) Incidentes comunes y accion inmediata

1. Sitio no seguro:
- Reinstalar `.cer` en clientes (Root CA).
- Verificar que se use `https://<SERVER_IP>:5067`.
- Si el navegador muestra certificado emitido a `localhost`, regenerar:

```powershell
.\scripts\https\New-LanCertificate.ps1 -ServerIp <SERVER_IP> -PersistToUserEnv
```

- Reiniciar con:

```powershell
.\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <SERVER_IP>
```

- Reimportar el nuevo `C:\BitacoraEvidencias\certs\bitacora-lan.cer` en `Entidades de certificacion raiz de confianza` del equipo cliente.
- Si el error es `NET::ERR_CERT_AUTHORITY_INVALID`, cerrar y reabrir el navegador despues de importar el `.cer` nuevo.

2. App no inicia:
- Ejecutar el lanzador manual:
```bat
C:\BitacoraEvidencias\Iniciar Bitacora Manual.cmd
```
- Revisar logs:
  - `C:\BitacoraEvidencias\deploy\logs\manual-start.log`
  - `C:\BitacoraEvidencias\deploy\logs\manual-app.err.log`
- Revisar variables de certificado:
  - `BITACORA_HTTPS_CERT_PATH`
  - `BITACORA_HTTPS_CERT_PASSWORD`
- Revisar variable de cifrado:
  - `BITACORA_DATA_PROTECTION_KEY`
- Confirmar existencia del `.pfx`.

3. La app muestra `enc:v1:...` o no cargan miniaturas:
- La aplicacion arranco sin la llave de cifrado o con una llave distinta.
- Validar:

```powershell
[Environment]::GetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "Machine")
```

- Reiniciar con:

```bat
.\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>
```

- Confirmar que el puerto `5067` queda escuchando y que abre `https://<SERVER_IP>:5067/Login`.

4. Usuario bloqueado:
- Esperar ventana de bloqueo o reset por Admin.
- Registrar seguimiento en bitacora.

5. Problema en archivado de auditoria:
- Revisar `C:\BitacoraEvidencias\AuditArchive\logs`.
- Revisar `C:\BitacoraEvidencias\AuditArchive\latest-run.json` y `C:\BitacoraEvidencias\AuditArchive\latest-status.json`.
- Ejecutar manualmente:

```powershell
.\scripts\audit\Archive-AuditLog.ps1
```

- Confirmar generacion de `AuditLog_YYYYMMDD.log` y que no existan errores en el log del proceso.

## 11) Cifrado y migracion manual

Documento tecnico:
- `docs/CIFRADO-DATOS-EVIDENCIAS.md`

Migracion manual de datos y evidencias existentes:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data" -StorageRoot "C:\BitacoraEvidencias\Evidencias"
```

La migracion requiere respaldo previo reciente. El flujo de publish ya genera respaldo previo; fuera de publish, ejecutar respaldo manual antes de cifrar o activar respaldo integrado:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data" -StorageRoot "C:\BitacoraEvidencias\Evidencias" -CreateBackup $true
```

## 12) Pendientes SGSI vigentes

Pendientes formales actuales:
- `docs/PENDIENTES-SGSI.md`

Documento de cierre SGSI:
- `docs/SGSI-CIERRE-ISO27001-27002.md`

