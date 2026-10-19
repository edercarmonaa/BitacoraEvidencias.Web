# Publicacion y despliegue SQL Server 0.2.0

Estado 19/10/2026: publicaciones y lanzador de ensayo disponibles; cambio productivo pendiente. No usar `Publish-Bitacora.ps1`, `Start-BitacoraManual.cmd`, `Start-BitacoraPublished.ps1` ni `Start-BitacoraWithHttps.ps1` para desplegar SQL: esos procedimientos configuran SQLite.

## Ensayo

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Publish-BitacoraSqlServer.ps1 -AppFolder App-ensayo-nuevo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlServer.ps1 -AppFolder App-ensayo-nuevo
```

El publicador rechaza carpetas existentes y rutas productivas; conserva binarios previos. El lanzador rechaza puerto 5067/ocupado y rutas productivas, solicita credenciales SQL, usa `BitacoraEvidencias_Pruebas` y copias de evidencias. Puerto predeterminado 5068. No sustituye el acceso directo SQLite ni cambia variables persistentes.

La publicacion de ensayo mas reciente comprobada es `App-backup-final`: incluye busquedas, validaciones y control de respaldo local. Los ajustes PowerShell posteriores se ejecutan desde el repositorio; no se incorporan automaticamente a un directorio de binarios.

## Condiciones para produccion

1. Completar [aceptacion](ESTADO-0.2.0.md), resolver los fallos relevantes y comprobar el usuario SQL de operacion.
2. Acordar mantenimiento sin escritores, incluyendo aplicaciones y tareas SQLite que modifiquen datos.
3. Guardar respaldo final SQLite/evidencias y copias de historicos, binarios, configuracion, certificados y secretos con custodia separada de la llave. Ver [migracion](MIGRACION-0.2.0.md).
4. Importar desde esa copia a la base productiva existente solo si esta vacia. No borrar bases, recrear usuarios ni usar la copia antigua de ensayo como corte final.
5. Preparar una publicacion SQL independiente y su configuracion productiva, puerto 5067 y evidencias existentes. Publish-BitacoraSqlRelease.ps1 y Start-BitacoraSqlProduction.ps1 ya preparan un despliegue SQL independiente; falta ensayar el arranque con usuario/permisos finales y aceptar el cambio del acceso directo. Los scripts SqlServer sin ese sufijo siguen siendo exclusivos de ensayo. Ver [SCRIPTS-0.2.0.md](SCRIPTS-0.2.0.md).
6. Validar login, roles, lectura de datos/imagenes, escrituras y respaldo antes de admitir usuarios.
7. En el cambio aprobado, deshabilitar las tareas SQLite y registrar/habilitar las SQL con sus credenciales locales. No ejecutar ambos proveedores como servicio productivo simultaneo.

## Continuidad

Conservar la llave actual y los hashes/sales de cuentas; no generar un admin nuevo al migrar. Git no conserva datos ni secretos. Antes de admitir escrituras se puede recuperar el estado consistente SQLite. Despues de nuevas escrituras SQL, volver requiere transferencia/conciliacion, no arrancar sin mas la base anterior.

El ciclo de respaldo SQL detiene/reinicia la misma publicacion mediante control local de la misma cuenta Windows, sin endpoint HTTP. Requiere proceso independiente y mismo nivel de elevacion que la tarea. No administra IIS ni servicios Windows. Ver [BACKUP-OPERACION.md](BACKUP-OPERACION.md).

Recuperacion en otra maquina: [RECUPERACION-SQLSERVER.md](RECUPERACION-SQLSERVER.md). Procedimientos conservados de SQLite: [SQLITE-0.1.md](SQLITE-0.1.md).
