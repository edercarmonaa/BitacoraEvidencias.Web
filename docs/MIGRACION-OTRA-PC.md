# Migracion a Otra PC (Guia Rapida)
Fecha: 2026-04-22

## Objetivo
Mover y probar Bitacora Evidencias en otra computadora sin perder datos (SQLite + evidencias) y mantener HTTPS en LAN.

## Alcance y limitacion actual

Esta guia deja preparada la recuperacion operativa en otra PC, pero hoy no existe un equipo alterno disponible de forma permanente. Por ello, la continuidad depende de conseguir un host sustituto y ejecutar este procedimiento.

## Rutas usadas

- Base operativa: `C:\BitacoraEvidencias`
- Scripts operativos: `C:\BitacoraEvidencias\scripts`
- App publicada: `C:\BitacoraEvidencias\deploy\current`
- Evidencias: `C:\BitacoraEvidencias\Evidencias`
- Base de datos: `C:\BitacoraEvidencias\Data\bitacora-evidencias.db`
- Backups: `C:\BitacoraEvidencias\BackupsBitacora`
- Certificados: `C:\BitacoraEvidencias\certs`
- Monitoreo: `C:\BitacoraEvidencias\Monitoreo`
- URL LAN esperada: `https://<IP_DESTINO>:5067`
- Llave de cifrado: `BITACORA_DATA_PROTECTION_KEY` en variable de entorno `Machine`

## Prerrequisitos en PC destino

1. Windows con PowerShell.
2. .NET 9 SDK instalado.
3. Puerto `5067/TCP` permitido en firewall.
4. Espacio suficiente para:
   - `deploy`
   - `Data`
   - `Evidencias`
   - backups temporales si se restauran desde ZIP
5. Usuario operativo con permisos sobre `C:\BitacoraEvidencias`.
6. Llave `BITACORA_DATA_PROTECTION_KEY` del equipo origen resguardada.

## Paso 1. Preparar origen

1. Detener app en origen.
2. Confirmar ultimo respaldo correcto:
   - `C:\BitacoraEvidencias\BackupsBitacora`
   - `C:\BitacoraEvidencias\BackupsBitacora\logs`
3. Si hace falta uno nuevo, ejecutar:

```powershell
C:\BitacoraEvidencias\scripts\backup\Backup-Bitacora.ps1
```

4. Copiar a USB, disco externo o medio seguro:
- `C:\BitacoraEvidencias\deploy`
- `C:\BitacoraEvidencias\scripts`
- `C:\BitacoraEvidencias\certs` si se quiere conservar material existente
- `C:\BitacoraEvidencias\Evidencias` si se migra historial completo
- ultimo ZIP de `C:\BitacoraEvidencias\BackupsBitacora`
- opcional: `C:\BitacoraEvidencias\Monitoreo` como evidencia operativa
- valor de `BITACORA_DATA_PROTECTION_KEY` por canal seguro, nunca dentro del ZIP si no esta protegido.

## Paso 2. Preparar destino

1. Crear estructura base:

```powershell
New-Item -ItemType Directory -Path C:\BitacoraEvidencias -Force
New-Item -ItemType Directory -Path C:\BitacoraEvidencias\Data -Force
New-Item -ItemType Directory -Path C:\BitacoraEvidencias\Evidencias -Force
New-Item -ItemType Directory -Path C:\BitacoraEvidencias\BackupsBitacora -Force
New-Item -ItemType Directory -Path C:\BitacoraEvidencias\scripts -Force
New-Item -ItemType Directory -Path C:\BitacoraEvidencias\deploy -Force
New-Item -ItemType Directory -Path C:\BitacoraEvidencias\certs -Force
```

2. Copiar scripts operativos a:
- `C:\BitacoraEvidencias\scripts`

3. Copiar despliegue publicado a:
- `C:\BitacoraEvidencias\deploy`

4. Restaurar o copiar base de datos a:
- `C:\BitacoraEvidencias\Data\bitacora-evidencias.db`

5. Copiar evidencias a:
- `C:\BitacoraEvidencias\Evidencias`

6. Si se reutilizaran certificados existentes, copiar a:
- `C:\BitacoraEvidencias\certs`

7. Configurar la llave de cifrado en destino:

```powershell
[Environment]::SetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "VALOR_ORIGINAL", "Machine")
```

Debe ser la misma llave usada en origen. No generar una llave nueva para una base/evidencias ya cifradas.

Si la BD no existe o se requiere crearla desde cero:

```powershell
C:\BitacoraEvidencias\scripts\db\Initialize-BitacoraDatabase.ps1
```

Si existe almacenamiento legado en `.\Storage` y el destino actual sera `C:\BitacoraEvidencias\Evidencias`:

```powershell
C:\BitacoraEvidencias\scripts\storage\Migrate-LegacyStorage.ps1
```

## Paso 3. Restaurar desde backup ZIP

Si se usara el ZIP como fuente principal de recuperacion:

1. Extraer el ultimo ZIP a carpeta temporal, por ejemplo:
- `C:\BitacoraEvidencias\RestoreTemp\YYYYMMDD_HHMMSS`

2. Restaurar desde el contenido extraido:
- `Data\bitacora-evidencias.db`
- `Data\bitacora-evidencias.db-wal` si existe
- `Data\bitacora-evidencias.db-shm` si existe
- `Evidencias\...`

3. Copiar esos archivos a:
- `C:\BitacoraEvidencias\Data`
- `C:\BitacoraEvidencias\Evidencias`

## Paso 4. Certificado HTTPS en destino

Generar certificado nuevo para la IP LAN del equipo destino:

```powershell
C:\BitacoraEvidencias\scripts\https\New-LanCertificate.ps1 -ServerIp <IP_DESTINO>
```

Este paso deja variables de entorno de certificado para Kestrel.

## Paso 5. Arranque recomendado (modo publicado manual)

Iniciar la app publicada manualmente:

```bat
C:\BitacoraEvidencias\scripts\deploy\Start-BitacoraManual.cmd <IP_DESTINO>
```

El launcher espera a que `5067/TCP` quede escuchando y abre:

```text
https://<IP_DESTINO>:5067/Login
```

No se registra tarea programada de inicio de sesion para la aplicacion. Si se requiere diagnostico tecnico, usar `Start-BitacoraPublished.ps1`.

## Paso 6. Arranque alterno (modo scripts/proyecto)

Si se necesita arrancar desde scripts y no desde el deploy publicado:

```powershell
C:\BitacoraEvidencias\scripts\https\Start-BitacoraWithHttps.ps1 -ServerIp <IP_DESTINO>
```

## Paso 7. Firewall

En destino, abrir `5067/TCP`:

```powershell
netsh advfirewall firewall add rule name="Bitacora HTTPS 5067" dir=in action=allow protocol=TCP localport=5067
```

## Paso 8. Certificado en clientes LAN

Si otros equipos abriran la URL LAN:

1. Exportar o ubicar el `.cer` generado en destino.
2. Instalarlo en cada cliente en:
- `Entidades de certificacion raiz de confianza (equipo local)`

## Paso 9. Validacion minima

1. Abrir:
- `https://<IP_DESTINO>:5067/Login`

2. Probar login.

3. Verificar:
- Oficios
- Busqueda
- Reportes
- Usuarios
- Bitacora

4. Abrir evidencia desde el sistema.
5. Confirmar que CURP, nombre, CCT y observaciones no aparecen como `enc:v1:...`.

6. Confirmar que rutas privadas redirigen a Login sin sesion.

7. Confirmar que el backup del origen sea visible o que el entorno destino ya pueda generar uno nuevo.

8. Ejecutar monitoreo basico:

```powershell
C:\BitacoraEvidencias\scripts\monitor\Monitor-Bitacora.ps1
```

9. Revisar:
- `C:\BitacoraEvidencias\Monitoreo\logs`
- `C:\BitacoraEvidencias\Monitoreo\latest-status.json`

## Nota

- La app ya no crea ni actualiza el esquema de forma implicita al arrancar.
- Si la BD destino esta vacia, primero ejecuta `C:\BitacoraEvidencias\scripts\db\Initialize-BitacoraDatabase.ps1`.
- La app ya no mueve almacenamiento legado `.\Storage` de forma implicita al arrancar.
- Si detectas esa carpeta legado, ejecuta `C:\BitacoraEvidencias\scripts\storage\Migrate-LegacyStorage.ps1`.
- Esa preparacion crea el usuario `admin` con contrasena temporal y cambio obligatorio en primer acceso.
- Si necesitas fijar esa contrasena inicial, define `Security__BootstrapAdminPassword` antes del primer arranque.

## Problemas comunes

1. No conecta:
- Revisar app levantada, IP correcta y firewall `5067`.

2. Sitio no seguro:
- Falta instalar `.cer` en cliente.
- Si el certificado visible en navegador dice `localhost`, regenerar en destino:

```powershell
C:\BitacoraEvidencias\scripts\https\New-LanCertificate.ps1 -ServerIp <IP_DESTINO> -PersistToUserEnv
```

- Reiniciar la app y reimportar el `.cer` nuevo en el cliente.

3. Error de certificado en servidor:
- Reejecutar `New-LanCertificate.ps1` con IP correcta.

4. No cargan evidencias:
- Revisar `Storage:RootPath` y permisos de carpeta.
- Si los datos aparecen como `enc:v1:...`, revisar que `BITACORA_DATA_PROTECTION_KEY` sea la misma del origen y reiniciar la app.

5. No genera backup:
- Revisar `BITACORA_BACKUP_ROOT`
- Revisar tarea `BitacoraEvidencias-Backup-Diario`
- Revisar `C:\BitacoraEvidencias\BackupsBitacora\logs`

6. Monitoreo en `ALERT`:
- Revisar `C:\BitacoraEvidencias\Monitoreo\latest-status.json`
- Validar conectividad a la URL LAN
- Confirmar que existe ZIP reciente

## Checklist corto

- [ ] Scripts operativos copiados en destino.
- [ ] App publicada copiada o republicada en destino.
- [ ] DB restaurada en `C:\BitacoraEvidencias\Data`.
- [ ] Evidencias copiadas en `C:\BitacoraEvidencias\Evidencias`.
- [ ] `BITACORA_DATA_PROTECTION_KEY` configurada en destino.
- [ ] Certificado generado en destino.
- [ ] App levantada en `https://<IP_DESTINO>:5067`.
- [ ] Clientes LAN confian en `.cer`.
- [ ] Login y modulos principales OK.
- [ ] Evidencias abren correctamente.
- [ ] Datos sensibles se muestran legibles dentro de la app.
- [ ] Monitoreo basico ejecutado sin errores criticos no atendidos.
