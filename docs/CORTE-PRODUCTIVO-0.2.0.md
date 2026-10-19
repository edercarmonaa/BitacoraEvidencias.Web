# Corte productivo de 0.2.0

19/10/2026. **SQL activo y aceptado funcionalmente; tareas SQL habilitadas y CHECKDB de la copia restaurada aprobado.** El responsable autorizo la ventana sin usuarios y confirmo `https://192.168.1.95:5067`. El certificado existente cubre esa IP y vence el 28/07/2029. La comprobacion SQL no sustituye la aceptacion de recuperacion integral de la aplicacion.

## Evidencia del corte

- SQLite detenida; tareas anteriores de backup, monitor y archivado deshabilitadas. No se eliminaron sus definiciones ni binarios.
- Codigo SQLite conservado en `v0.1.0` y `archive/sqlite-0.1`, ambos en `6aba98e07e52775571d8ae128f5b5cc208891386`.
- Suite completa previa: 132 pruebas aprobadas. Adaptacion guardada en `bae60e7`.
- Respaldo final SQLite `BitacoraBackup_20261019_134546.zip`, 633.66 MB, conservado en carpeta privada de corte bajo `C:\BitacoraEvidencias\BackupsBitacora`. SHA256: `A0C35E910058FF5EFA901B4CF0CB5A37E87562319AD921E2805C95AD1F887750`.
- Custodia adicional: historicos, despliegue/scripts SQLite, certificado, anillo ASP.NET y configuracion/credencial protegidas con DPAPI. Los secretos no se incluyen en Git ni en el ZIP de datos.
- Extraccion nueva fuera de produccion: 3,660 archivos comparados con el ZIP. Las 3,656 fotografias/miniaturas son un subconjunto; los totales no son intercambiables.
- Preflight aprobado; base SQL productiva comprobada vacia con la cuenta de operacion antes de importar.
- Importacion transaccional y verify completos aprobados: 6 usuarios, 824 oficios, 893 casos, 1,828 filas de evidencias y 1,909 eventos de auditoria. Estos conteos corresponden al corte, antes de nuevas escrituras.
- Release y scripts SQL separados de `deploy\current`; la instalacion anterior se conserva. Nuevo lanzador `Start-BitacoraSqlInstalled.ps1` probado con ValidateOnly y entorno inicialmente sin secretos: recuperacion DPAPI y restauracion del entorno aprobadas, sin arranque.
- Tareas SQL registradas inicialmente deshabilitadas y luego habilitadas tras la revision funcional y operativa: monitor 09:15, archivo 09:20 y backup 14:00 de Mexico. Siguientes ejecuciones: 20/10/2026. Tareas SQLite conservadas y deshabilitadas.

## Cierre funcional y operativo

El primer respaldo SQL completo manual termino: 3,656 evidencias y 24 historicos comparados, BAK/VERIFYONLY/cabecera y ZIP verificados. La cuenta SQL de operacion no tenia permiso de VERIFYONLY. El responsable autorizo expresamente crear BitacoraRespaldo con BACKUP DATABASE y CREATE ANY DATABASE para la verificacion; se creo sin sysadmin, con clave aleatoria protegida con DPAPI y pruebas reales de BACKUP/VERIFYONLY/HEADERONLY aprobadas. La tarea de respaldo ya usa esa credencial separada. No se elevaron los permisos de la cuenta web.

SQL escucha en 192.168.1.95:5067; /Login devuelve HTTP 200 con validacion HTTPS normal de Windows. El acceso C:\BitacoraEvidencias\Iniciar Bitacora Manual.cmd apunta al lanzador SQL; el original SQLite esta en custodia. El responsable confirmo ingreso y consultas de inicio, Busquedas, Auditoria y fotografia/miniatura.

La tarea real de respaldo se ejecuto el 19/10 a las 14:11 durante la ventana autorizada: cierre ordenado, BACKUP/VERIFYONLY/cabecera, 3,656 evidencias y 24 historicos comparados, ZIP confirmado a las 14:14 y reinicio. Task Scheduler devolvio resultado 0 y estado Ready, con siguiente backup el 20/10 a las 14:00. HTTPS volvio a responder 200. Se comprobo en memoria que la instancia reiniciada conserva exactamente la misma llave, base SQL y carpeta de evidencias; no se imprimieron ni persistieron secretos del canal de control.

Monitor: disponibilidad, SQL y respaldo reciente OK; alerta real de capacidad, 20.15 GB libres frente al umbral de 25 GB en la primera medicion. Auditoria Preview: 1,900 elegibles, cero exportados/retirados. No se ejecuto Archive durante el corte.

## Correccion posterior de Auditoria

19/10: Auditoria y Oficios pendientes aceptan numeroOficio ausente/vacio, evitando el Required implicito de MVC. Cambio edc2a10, suite completa 137 aprobadas; nuevo release SQL publicado y HTTPS 200 despues del reinicio ordenado. Release SQL anterior conservado; no se aplicaron migraciones ni cambios de datos/secretos. La comprobacion visual del filtro corregido queda a cargo del responsable al recargar.

## Integridad del respaldo SQL: pendiente cerrado

19/10/2026: se extrajo nuevamente el BAK del ZIP productivo `BitacoraSqlBackup_BitacoraEvidencias_20261019_135819_4f035e7276854b21acb09ccedc4af7f9.zip` y se restauro con CHECKSUM y archivos nuevos en una base temporal unica. **DBCC CHECKDB con NO_INFOMSGS, ALL_ERRORMSGS y MAXDOP=1 termino correctamente**, sin opciones de reparacion ni PHYSICAL_ONLY/NOINDEX. Coincidieron los conteos del corte: 6 usuarios, 824 oficios, 893 casos, 1,828 evidencias y 1,909 eventos de auditoria. La base temporal se elimino despues de comprobar su marcador de propiedad; no se modifico ni detuvo produccion.

Los dos intentos anteriores (normal y MAXDOP=1) no habian terminado por RESOURCE_SEMAPHORE y timeout; se conservan como antecedentes, no como resultado actual. Al retomar la prueba, Windows tenia aproximadamente 1,050 MB disponibles, SQL ya no reportaba memoria fisica baja y su concesion de consultas disponia de unos 576 MB. No se cambiaron parametros globales del servidor para completar la comprobacion.

## Limites pendientes

- Conservar alerta de espacio y revisar capacidad sin borrar respaldos automaticamente.
- Completar aceptacion visual especifica de validaciones/confirmaciones de formularios y recuperacion integral en copia. El acceso productivo y las consultas ya fueron confirmados.
- Custodiar secretos por un mecanismo recuperable externo. Los archivos DPAPI preparados sirven para la misma identidad Windows y equipo; no sustituyen un respaldo portable para otro equipo.
- La revision de integridad SQL ya aprobo. La version declarada de la aplicacion es 0.2.0; la etiqueta v0.2.0 y la publicacion remota no se han creado como parte de esta comprobacion.

Las rutas exactas operativas y el avance tecnico se guardan localmente en `tmp/production-cutover-state.json` y `tmp/production-deployment-state.json`, excluidos de Git. No ejecutar import otra vez: SQL ya contiene el corte verificado. No volver a SQLite sin revisar primero si SQL admitio nuevas escrituras.
