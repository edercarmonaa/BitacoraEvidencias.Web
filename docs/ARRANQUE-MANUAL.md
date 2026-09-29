# Arranque Manual - Bitacora de Evidencias

Fecha de actualizacion: 2026-05-11

## Decision vigente

La aplicacion ya no se inicia automaticamente al reiniciar Windows ni al iniciar sesion.

Se elimino el enfoque de tarea programada de inicio de sesion porque en la operacion real podia ejecutar mas de un arranque y dejar el proceso sin escuchar en el puerto `5067`.

## Estado final

- No existe tarea programada de inicio de sesion para la aplicacion.
- No existe archivo de inicio automatico en la carpeta Startup del usuario.
- No se conservan scripts `Start-BitacoraStartup.cmd` ni `Run-BitacoraApp.cmd` en `C:\BitacoraEvidencias\deploy`.
- El arranque diario se realiza manualmente.

## Arranque manual

Desde el repositorio:

```bat
.\scripts\deploy\Start-BitacoraManual.cmd <SERVER_IP>
```

En la PC operativa se dejaron accesos convenientes:

```text
C:\BitacoraEvidencias\Iniciar Bitacora Manual.cmd
Escritorio del usuario operativo\Iniciar Bitacora Manual.cmd
```

El launcher manual:

- Usa el release publicado en `C:\BitacoraEvidencias\deploy\current`.
- Configura HTTPS en `https://<SERVER_IP>:5067`.
- Usa `BITACORA_HTTPS_CERT_PATH`, `BITACORA_HTTPS_CERT_PASSWORD` y `BITACORA_DATA_PROTECTION_KEY`.
- Espera a que el puerto `5067` quede escuchando.
- Abre `https://<SERVER_IP>:5067/Login`.

## Logs

Los logs del arranque manual quedan en:

```text
C:\BitacoraEvidencias\deploy\logs\manual-start.log
C:\BitacoraEvidencias\deploy\logs\manual-app.out.log
C:\BitacoraEvidencias\deploy\logs\manual-app.err.log
```

## Scripts conservados

- `scripts\deploy\Publish-Bitacora.ps1`: publica el release.
- `scripts\deploy\Start-BitacoraManual.cmd`: arranque operativo manual.
- `scripts\deploy\Start-BitacoraPublished.ps1`: arranque tecnico de diagnostico.
