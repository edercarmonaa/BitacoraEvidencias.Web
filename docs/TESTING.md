# Pruebas y evidencia de 0.2.0

Resultados a 19/10/2026. Una comprobacion automatica, una restauracion SQL y la aceptacion visual son evidencias distintas. No ejecutar smokes, importacion o archivado con borrado sobre produccion para comprobar cambios.

## Pruebas .NET

```powershell
dotnet test tests/BitacoraEvidencias.Web.Tests/BitacoraEvidencias.Web.Tests.csproj -c Release
```

Ultima suite completa (Debug, 19/10/2026): **137 aprobadas, cero fallos y cero omitidas**, ejecutada con `dotnet test tests/BitacoraEvidencias.Web.Tests/BitacoraEvidencias.Web.Tests.csproj --no-restore`. Incluye cinco regresiones nuevas del filtro opcional de Auditoria: metadatos MVC en ambas paginas y carga sin filtro nulo/vacio/espacios. Las ocho pruebas dirigidas de Auditoria aprobaron. La suite anterior de 132 casos y la de 115 (109 aprobados/seis fallos) quedan como evidencia historica.

Se adaptaron los fixtures de fotografias para usar una variable de clave unica por instancia, con clave sintetica y limpieza al terminar, sin leer ni sustituir la clave real. Las operaciones de lectura, rutas, eliminacion, reordenamiento y reversion se ejecutan con nombres antiguos y protegidos; se comprueba ademas lectura de archivos antiguos y cambio de ruta antigua con proteccion habilitada.

La prueba de creacion del administrador SQLite ahora prepara el esquema de prueba con EnsureCreated y habilita expresamente la compatibilidad legacy. Otra prueba exige rechazar ese flujo sin autorizacion de compatibilidad y conservar vacia la tabla de usuarios. Esto separa el bootstrap de las migraciones SQLite congeladas: no actualiza su snapshot, no suprime PendingModelChangesWarning ni acredita crear una base nueva mediante esas migraciones. SqlServerMigrationTests mantiene la comprobacion independiente de esquema SQL y ausencia de cambios pendientes en su modelo.

SQLite en pruebas de servicios no significa que Program.cs permita seleccionar ese proveedor en 0.2.0. El contexto operativo es SQL. Siguen pendientes la aceptacion visual, recuperacion integral y revision de dependencias; una suite aprobada no autoriza el cambio productivo.

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

Test-ScriptAlignment.ps1 comprueba sintaxis de los scripts, rechazo previo de reset/publicacion SQLite, objetivos smoke, carpetas HMAC, imagen sintetica, privacidad de staging, TLS explicito y lanzador SQL con procesos simulados. Ultima ejecucion: 70 comprobaciones, incluyendo sintaxis del lanzador instalado, sin conectar a SQL ni iniciar aplicaciones. Se repitieron las 39 comprobaciones de backup tras separar las etiquetas de diagnostico BACKUP/VERIFYONLY/HEADERONLY.

## Formularios

30 comprobaciones DOM aprobadas, sin navegador real. Herramienta de prueba separada, no dependencia de produccion:

```powershell
npm install --prefix tmp/form-dom-tests --no-save --ignore-scripts happy-dom@21.0.3
node scripts/validation/Test-FormFeedback.cjs
```

Requiere Node 22.20+/24 y npm compatibles con esa herramienta. Para aceptacion visual seguir [VALIDACIONES-0.2.0.md](VALIDACIONES-0.2.0.md): blur, toast, historicos incompletos, fechas, CCT antiguas, HTMX y confirmaciones aceptar/cancelar.

## Pruebas reales registradas

Los resultados siguientes corresponden al ensayo previo. Para produccion ver el [registro del corte](CORTE-PRODUCTIVO-0.2.0.md): import/verify y ciclo real de backup/reinicio aprobados, ingreso/consultas confirmados; CHECKDB completo del nuevo BAK restaurado con MAXDOP=1 y cinco conteos del corte aprobados. Las copias temporales propias se eliminaron sin intervenir produccion.

- Import/verify iniciales: cinco tablas y todos sus campos coincidentes con la copia SQLite antes de escrituras.
- Busquedas: handler sin filtros bajo de unos 25 s a 347 ms inicial/27 ms repeticion; responsable confirmo funcionamiento. Ver [PERFORMANCE-0.2.0.md](PERFORMANCE-0.2.0.md).
- Restauracion BAK extraido del ZIP en base temporal, DBCC CHECKDB sin incidencias y conteos iguales. Validacion integral posterior de cuentas/fotos recuperadas sigue siendo necesaria para aceptar continuidad.
- Exportacion de 1,900 registros reales de ensayo sin retirar ninguno; retirada y fallo de DELETE probados solo con datos sinteticos temporales.
- Backup con 3,656 archivos de evidencias y 20 historicos/manifiestos, SHA256 verificado. Reinicio tras fallo de backup y monitor HTTP 200 probados con instancia aislada.

## Smoke y aceptacion

Usar [SMOKE-TEST-INTEGRAL.md](SMOKE-TEST-INTEGRAL.md) solo sobre copias. Los smokes pueden crear, cambiar y borrar datos e invalidan verify contra el origen. Suministrar la contrasena por BITACORA_SMOKE_ADMIN_PASSWORD de proceso, sin argumentos ni logs; validan HTTPS por defecto; TrustLocalHttpsCertificate es una excepcion explicita de laboratorio y no demuestra confianza del certificado. Ambos recorridos SQL sinteticos actualizados pasaron 47 pruebas y un SKIP cada uno; ver [SCRIPTS-0.2.0.md](SCRIPTS-0.2.0.md).

Los resultados antiguos de 2026-02/03 y los totales 66/76/83/110 corresponden a etapas anteriores, no al estado actual. El [estado de aceptacion](ESTADO-0.2.0.md) concentra los pendientes; ningun resultado parcial autoriza automaticamente el cambio productivo.
