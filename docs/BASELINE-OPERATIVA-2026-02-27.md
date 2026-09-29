# Baseline Operativa Final
Fecha: 2026-02-27

## Estado
- Baseline funcional validada en QA manual.
- Resultado QA: `Q01` a `Q32` en estado `OK`.
- URL operativa objetivo: `https://<SERVER_IP>:5067`
- Release desplegado: `20260227_124301`
- Proceso activo esperado: `BitacoraEvidencias.Web.exe` escuchando en `<SERVER_IP>:5067`
- Arranque vigente: manual, sin tarea programada de inicio de sesion para la aplicacion.

## Version base
- Framework: .NET 9
- App: ASP.NET Core Razor Pages + SQLite + Kestrel
- Seguridad activa:
  - HTTPS LAN estricto
  - Roles Admin/Capturista
  - Lockout 5 intentos / 30 min
  - Sesion 30 min inactividad
  - Evidencias fuera de `wwwroot`
  - Cifrado aplicativo de datos sensibles y evidencias cuando `BITACORA_DATA_PROTECTION_KEY` esta configurada
  - AuditLog obligatorio

## Despliegue aprobado (modo publicado)
1. Publicar release:
```powershell
.\scripts\deploy\Publish-Bitacora.ps1
```

2. Iniciar manualmente desde release publicado:
```bat
.\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>
```

## Rutas
- Releases: `C:\BitacoraEvidencias\deploy\releases`
- Release activo: `C:\BitacoraEvidencias\deploy\current`
- Evidencias: `C:\BitacoraEvidencias\Evidencias`
- Base de datos produccion: `C:\BitacoraEvidencias\Data\bitacora-evidencias.db`

## Nota de operacion
- `Publish-Bitacora.ps1` ejecuta respaldo previo por defecto, conserva ultimos releases y permite decidirlo con `-CreateBackup true`, `-CreateBackup false` o `-NoBackup`.
- Para personalizar el respaldo del publish se puede usar `-BackupRoot "C:\BitacoraEvidencias\BackupsBitacora" -BackupRetentionDays 30`.
- El inicio publicado usa certificado local (`BITACORA_HTTPS_CERT_PATH` / `BITACORA_HTTPS_CERT_PASSWORD`).
- El inicio publicado requiere `BITACORA_DATA_PROTECTION_KEY` para datos/evidencias cifrados.
- `Start-BitacoraPublished.ps1` se conserva como arranque tecnico de diagnostico; la operacion diaria usa `Start-BitacoraManual.cmd`.

