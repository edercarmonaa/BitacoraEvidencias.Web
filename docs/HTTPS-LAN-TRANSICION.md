# HTTPS LAN (Modo Estricto)

Estado actual configurado:
- Solo HTTPS en `https://<SERVER_IP>:5067`
- Cookies seguras obligatorias
- Redireccion HTTPS habilitada
- Sin endpoint HTTP en arranque normal

## 1) Configuracion de seguridad aplicada

En `appsettings.json` y `appsettings.Development.json`:

```json
"Security": {
  "RequireHttpsCookies": true,
  "EnableHttpsRedirection": true
}
```

## 2) Perfil de arranque local

`Properties/launchSettings.json`:
- Perfil unico `https-lan`
- `applicationUrl`: `https://<SERVER_IP>:5067`

## 3) Arranque recomendado en servidor (sin HTTP)

```powershell
.\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <SERVER_IP>
```

Notas:
- Usa certificado desde variables `BITACORA_HTTPS_CERT_PATH` y `BITACORA_HTTPS_CERT_PASSWORD`.
- Por defecto arranca con `ASPNETCORE_ENVIRONMENT=Production`.
- Para pruebas puedes cambiar entorno:

```powershell
.\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <SERVER_IP> -EnvironmentName Development
```

## 4) Validaciones rapidas

1. `https://<SERVER_IP>:5067/Login` abre sin alerta de certificado (si el CER esta confiado en cliente).
2. `http://<SERVER_IP>:5067` no debe funcionar (sin listener HTTP).
3. Login/logout operan correctamente.
4. Bitacora sigue registrando eventos.

## 5) Diagnostico rapido si Chrome muestra "No seguro"

1. Abrir el visor del certificado desde el navegador.
2. Revisar `Emitido a`:
- Si dice `localhost`, el servidor esta entregando un certificado incorrecto.
- Regenerar el certificado con la IP LAN exacta:

```powershell
.\scripts\https\New-LanCertificate.ps1 -ServerIp <SERVER_IP> -PersistToUserEnv
```

3. Reiniciar la app con:

```powershell
.\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <SERVER_IP>
```

4. Reimportar en cada cliente el nuevo `C:\BitacoraEvidencias\certs\bitacora-lan.cer` en:
- `Certificados (equipo local)`
- `Entidades de certificacion raiz de confianza`

5. Cerrar completamente Chrome y abrir de nuevo.

Referencia de sintomas:
- `NET::ERR_CERT_AUTHORITY_INVALID`: el cliente no confia en el `.cer` actual o sigue usando el anterior.
- Certificado emitido a `localhost`: el `.pfx` activo no corresponde a la IP LAN configurada.

## 6) Operacion diaria

- Ejecutar siempre por script HTTPS.
- Evitar `dotnet run` con perfiles HTTP heredados.
- Si se renueva certificado, redistribuir `.cer` a clientes.

