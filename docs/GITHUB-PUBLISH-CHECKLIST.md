# Checklist antes de publicar en GitHub

## No subir

- Bases SQLite reales: `Data/*.db`, `Data/*.db-wal`, `Data/*.db-shm`.
- Evidencias, fotografias, respaldos, ZIPs, exports o archivos importados.
- Certificados, llaves privadas o passwords: `*.pfx`, `*.pem`, `*.key`, `*.cer`.
- Logs de build, monitoreo, respaldo, smoke tests o diagnostico.
- Reportes operativos internos con IPs, rutas, usuarios, resultados de pruebas o riesgos.
- IPs internas reales, nombres de equipo, nombres de usuario o rutas locales personales.
- `appsettings.Production.json`, `appsettings.Local.json` o cualquier archivo con credenciales.

## Configuracion segura

- Mantener credenciales y llaves en variables de entorno:
  - `BITACORA_DATA_PROTECTION_KEY`
  - `BITACORA_HTTPS_CERT_PATH`
  - `BITACORA_HTTPS_CERT_PASSWORD`
- Usar `appsettings.json` solo con valores genericos o de desarrollo local.
- Usar `<SERVER_IP>` en documentacion publica y pasar la IP real solo al ejecutar scripts.

## Verificacion previa

Ejecutar busquedas antes del primer push:

```powershell
rg -n -i "password|secret|token|api[_-]?key|private key|BEGIN .*PRIVATE|pfx|BITACORA_.*PASSWORD|BITACORA_.*KEY" .
rg -n "\b\d{1,3}(?:\.\d{1,3}){3}\b" .
rg -n "C:\\Users\\|C:\\\\Users\\\\" .
Get-ChildItem -Recurse -File -Include *.db,*.pfx,*.pem,*.key,*.zip,*.bak,*.log
```

Si el proyecto ya tuvo commits locales con secretos, no basta con `.gitignore`: hay que eliminar esos archivos del historial antes de publicar.
