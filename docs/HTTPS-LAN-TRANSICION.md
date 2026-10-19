# HTTPS y TLS SQL en 0.2.0

La aplicacion mantiene cookies seguras y redireccion HTTPS en appsettings. El perfil local actual usa https://127.0.0.1:5068 con Development; el lanzador de ensayo usa Production y certificado externo. Produccion SQLite conserva su arranque 5067 hasta el cambio aprobado.

## Ensayo con certificado existente

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlServer.ps1 -AppFolder App-backup-final
```

BITACORA_HTTPS_CERT_PATH y BITACORA_HTTPS_CERT_PASSWORD deben estar disponibles en el proceso. El certificado debe cubrir el nombre/direccion usados; ServerAddress permite indicar uno cubierto. No desactivar seguridad para ocultar un error del navegador.

Los scripts Start-BitacoraWithHttps.ps1/Start-BitacoraPublished.ps1 anteriores tambien configuran SQLite; no son lanzadores SQL de esta rama. La generacion de certificados es una tarea distinta del cambio de proveedor: conservar el certificado actual salvo necesidad de renovacion revisada, y no confundirlo con la llave de cifrado de datos.

## Dos conexiones diferentes

| Conexion | Comprobacion |
| --- | --- |
| Navegador a Kestrel | Certificado HTTPS, nombre/SAN, vigencia y cadena confiable en cliente. |
| Aplicacion/scripts a SQL | Encrypt=True y confianza/identidad del certificado SQL. |

TrustLocalSqlCertificate afecta solo a SQL local: omite su comprobacion de identidad pero mantiene cifrado. No modifica el certificado HTTPS ni debe interpretarse como confianza ya resuelta para produccion. El monitor mantiene la validacion HTTPS; los smokes tambien validan confianza por defecto y solo la omiten con TrustLocalHttpsCertificate explicito, que no sirve para certificarla.

Comprobar que HTTPS abre sin alertas, login/roles funcionan y no hay listener HTTP no previsto. Si cambia equipo/nombre/IP, revisar certificado y clientes antes de habilitar usuarios. Nunca publicar PFX, contrasena ni llave. Recuperacion: [RECUPERACION-SQLSERVER.md](RECUPERACION-SQLSERVER.md).
