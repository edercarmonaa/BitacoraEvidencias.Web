# Pruebas y evidencia de 0.2.0

Resultados a 19/10/2026. Una comprobacion automatica, una restauracion SQL y la aceptacion visual son evidencias distintas. No ejecutar smokes, importacion o archivado con borrado sobre produccion para comprobar cambios.

## Pruebas .NET

```powershell
dotnet test tests/BitacoraEvidencias.Web.Tests/BitacoraEvidencias.Web.Tests.csproj -c Release
```

La ultima suite completa registrada ejecuto 115 pruebas: 109 aprobadas y seis fallos previos (cinco de fixtures/rutas de almacenamiento y uno de preparacion SQLite/modelo). Despues se agrego una prueba dirigida de configuracion de evidencias y pasaron las seis de BackupRuntimeSnapshotTests. No se volvio a ejecutar toda la suite tras esa adicion: no presentar 116 como total completo aprobado.

SQLite en pruebas de servicios no significa que Program.cs permita seleccionar ese proveedor en 0.2.0. El contexto operativo es SQL. Investigar los seis fallos y repetir la suite antes de aceptar la liberacion; no quedan resueltos por actualizar documentos.

## Scripts

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validation/Test-SqlServerConnectionScripts.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validation/Test-SqlBackup.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validation/Test-SqlOperations.ps1
```

Backup: 39 comprobaciones. Operaciones: 31 en la ejecucion con Integration, incluyendo tareas simuladas, archivo transaccional y bloqueo compartido/exclusivo. El modo local no incluye las comprobaciones SQL. Integracion requiere permisos para crear/eliminar solo una base temporal propia con datos sinteticos:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/validation/Test-SqlOperations.ps1 -Integration
```

No registra tareas reales. Los test generan artefactos bajo tmp, ignorado por Git. Run-StartupValidation.ps1 valida el flujo SQLite anterior; no usarlo como prueba del arranque SQL.

## Formularios

30 comprobaciones DOM aprobadas, sin navegador real. Herramienta de prueba separada, no dependencia de produccion:

```powershell
npm install --prefix tmp/form-dom-tests --no-save --ignore-scripts happy-dom@21.0.3
node scripts/validation/Test-FormFeedback.cjs
```

Requiere Node 22.20+/24 y npm compatibles con esa herramienta. Para aceptacion visual seguir [VALIDACIONES-0.2.0.md](VALIDACIONES-0.2.0.md): blur, toast, historicos incompletos, fechas, CCT antiguas, HTMX y confirmaciones aceptar/cancelar.

## Pruebas reales registradas

- Import/verify iniciales: cinco tablas y todos sus campos coincidentes con la copia SQLite antes de escrituras.
- Busquedas: handler sin filtros bajo de unos 25 s a 347 ms inicial/27 ms repeticion; responsable confirmo funcionamiento. Ver [PERFORMANCE-0.2.0.md](PERFORMANCE-0.2.0.md).
- Restauracion BAK extraido del ZIP en base temporal, DBCC CHECKDB sin incidencias y conteos iguales. Validacion integral posterior de cuentas/fotos recuperadas sigue siendo necesaria para aceptar continuidad.
- Exportacion de 1,900 registros reales de ensayo sin retirar ninguno; retirada y fallo de DELETE probados solo con datos sinteticos temporales.
- Backup con 3,656 archivos de evidencias y 20 historicos/manifiestos, SHA256 verificado. Reinicio tras fallo de backup y monitor HTTP 200 probados con instancia aislada.

## Smoke y aceptacion

Usar [SMOKE-TEST-INTEGRAL.md](SMOKE-TEST-INTEGRAL.md) solo sobre copias. Los smokes pueden crear, cambiar y borrar datos e invalidan verify contra el origen. Suministrar la contrasena por BITACORA_SMOKE_ADMIN_PASSWORD de proceso, sin argumentos ni logs; usan una excepcion de confianza HTTPS para laboratorio y no demuestran confianza del certificado.

Los resultados antiguos de 2026-02/03 y los totales 66/76/83/110 corresponden a etapas anteriores, no al estado actual. El [estado de aceptacion](ESTADO-0.2.0.md) concentra los pendientes; ningun resultado parcial autoriza automaticamente el cambio productivo.
