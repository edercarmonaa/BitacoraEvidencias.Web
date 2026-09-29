# Cifrado de Datos y Evidencias

Fecha de actualizacion: 04/05/2026

## Objetivo

Documentar el esquema implementado para proteger datos sensibles dentro de la base SQLite y archivos de evidencias almacenados en disco.

## Alcance

El cifrado aplica cuando la aplicacion arranca con la variable de entorno:

```powershell
BITACORA_DATA_PROTECTION_KEY
```

La llave debe ser la misma en operacion normal y en migraciones. Si se pierde o se cambia, no sera posible descifrar datos ni evidencias ya protegidos.

## Campos protegidos

En `CasosCorreccion` se cifran a nivel aplicacion:

- `Curp`
- `NombreCompleto`
- `Cct`
- `Folio`
- `EvidenciaUrl`
- `Observaciones`
- `Validador`

Para busqueda exacta se agregaron indices ciegos con HMAC:

- `CurpHash`
- `CctHash`
- `FolioHash`

La busqueda por CURP/CCT/Folio deja de depender de `LIKE` cuando el cifrado esta activo y usa coincidencia exacta por hash.

## Auditoria

`AuditLog` ya no conserva CURP/CCT/Folio en claro cuando la llave esta configurada. En su lugar conserva:

- hash HMAC para correlacion tecnica;
- sufijo visible para apoyo operativo, por ejemplo `***1234`.

Campos agregados:

- `CurpHash`, `CurpSuffix`
- `CctHash`, `CctSuffix`
- `FolioCertificadoHash`, `FolioCertificadoSuffix`

## Evidencias

Las evidencias y miniaturas se cifran con AES-GCM al guardarse.

- El archivo fisico queda con prefijo `BITACORA-ENC-V1`.
- La aplicacion descifra en memoria al visualizar.
- Las imagenes no deben abrirse como imagen normal si se revisan directamente desde disco.
- Si se ven normales desde la aplicacion, es porque el endpoint autenticado las descifra para el navegador.

## Nombres de carpetas

Con la llave configurada, las carpetas nuevas de oficio usan un identificador derivado por HMAC en lugar del numero de oficio legible. Esto reduce exposicion de estructura operativa en disco.

## Variable de entorno

Configuracion permanente recomendada en el servidor:

```powershell
[Environment]::SetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "VALOR_BASE64_DE_32_BYTES", "Machine")
```

Validacion:

```powershell
[Environment]::GetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "Machine")
```

El arranque manual publicado `scripts/deploy/Start-BitacoraManual.cmd <SERVER_IP>` pasa la variable `BITACORA_DATA_PROTECTION_KEY` al proceso de la aplicacion. Para diagnostico tecnico, `scripts/deploy/Start-BitacoraPublished.ps1` tambien lee la llave de `Process`, `Machine` o `User`, y debe mostrar:

```text
Llave de cifrado: configurada (Machine)
```

## Migracion manual de datos existentes

La migracion de datos y evidencias existentes se ejecuta manualmente:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data" -StorageRoot "C:\BitacoraEvidencias\Evidencias"
```

Por defecto no genera respaldo adicional, asumiendo que ya existe respaldo previo del publish. Para ordenar respaldo antes de cifrar:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data" -StorageRoot "C:\BitacoraEvidencias\Evidencias" -CreateBackup $true
```

Tambien se puede indicar destino y retencion:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data" -StorageRoot "C:\BitacoraEvidencias\Evidencias" -CreateBackup $true -BackupRoot "C:\BitacoraEvidencias\BackupsBitacora" -BackupRetentionDays 30
```

Si la base no se llama `bitacora-evidencias.db`, indicar ruta completa:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\security\Protect-ExistingData.ps1 -DatabasePath "C:\BitacoraEvidencias\Data\NOMBRE.db" -StorageRoot "C:\BitacoraEvidencias\Evidencias"
```

El script imprime:

- proyecto usado;
- base SQLite usada;
- carpeta de evidencias usada;
- evidencias revisadas;
- evidencias cifradas;
- evidencias sin cifrar;
- ejemplos de archivos no cifrados si existen.

## Respaldo previo

Antes de ejecutar la migracion debe existir respaldo reciente de:

- base SQLite;
- archivos `-wal` y `-shm` si existen;
- carpeta de evidencias.

La politica operativa vigente indica que `Publish-Bitacora.ps1` ejecuta respaldo previo antes de publicar. Si se ejecuta la migracion fuera del flujo de publish, se debe generar respaldo manual o usar `-CreateBackup $true` en el script de migracion:

```powershell
.\scripts\backup\Backup-Bitacora.ps1
```

## Validacion posterior

1. Reiniciar la aplicacion con `Start-BitacoraManual.cmd <SERVER_IP>`.
2. Confirmar que abre `https://<SERVER_IP>:5067/Login` y que el puerto `5067` queda escuchando.
3. Abrir detalle de oficio/caso y confirmar que CURP, nombre, CCT, observaciones y validador se muestran legibles.
4. Abrir evidencias desde la aplicacion y confirmar que cargan.
5. Abrir un archivo fisico de evidencia desde disco y confirmar que no abre como imagen normal.
6. Revisar SQLite y confirmar que los campos sensibles se almacenan como `enc:v1:...`.

## Riesgo operativo principal

El mayor riesgo es arrancar la aplicacion sin `BITACORA_DATA_PROTECTION_KEY` despues de haber cifrado datos. En ese caso:

- los campos apareceran como `enc:v1:...`;
- las miniaturas/evidencias no cargaran;
- la correccion es configurar la misma llave y reiniciar la aplicacion.
