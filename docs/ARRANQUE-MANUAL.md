# Arranque manual por version

Actualizado: 19/10/2026. No se agrega inicio automatico de la aplicacion al iniciar sesion en Windows. Las tareas SQL de mantenimiento tampoco sustituyen su arranque diario.

## Ensayo SQL 0.2.0

La publicacion comprobada mas reciente es App-backup-final. Con el puerto 5068 libre y las variables de llave/certificado disponibles:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlServer.ps1 -AppFolder App-backup-final
```

Solicita usuario/contrasena SQL, usa BitacoraEvidencias_Pruebas e Input/Evidencias y arranca Production/HTTPS 5068. Ctrl+C detiene solo ese ensayo. No cambia variables persistentes. Para publicar otros cambios, usar una carpeta nueva con Publish-BitacoraSqlServer.ps1; no repetir import.

TrustLocalSqlCertificate solo admite explicitamente un certificado SQL local sin confianza configurada. Si HTTPS cubre otro nombre/direccion usar ServerAddress; no ocultar errores HTTPS ni regenerar la llave de datos.

## SQLite productiva 0.1.0

El arranque anterior sigue apuntando a los binarios publicados en C:\BitacoraEvidencias\deploy\current y puerto 5067. Los scripts Start-BitacoraManual.cmd y Start-BitacoraPublished.ps1 configuran SQLite. Conservar su acceso directo, binarios, configuracion y logs; no cambiarlos por el ensayo SQL.

La version congelada y procedimientos originales se consultan en [SQLITE-0.1.md](SQLITE-0.1.md). No publicar el codigo SQL con Publish-Bitacora.ps1 ni ejecutar Start-BitacoraWithHttps.ps1 de esta rama suponiendo que preparara SQL.

## Produccion SQL pendiente

El lanzador Start-BitacoraSqlProduction.ps1 esta preparado y se probo su configuracion sin conectar/iniciar; falta ensayar la cuenta final, cambiar el acceso directo y aprobar la migracion; [DEPLOYMENT.md](DEPLOYMENT.md) detalla sus condiciones. El backup SQL puede cerrar/reiniciar una instancia compatible que ya este activa, conservando configuracion en memoria; si estaba detenida no la inicia. No es un servicio Windows ni recupera automaticamente un reinicio del equipo.
