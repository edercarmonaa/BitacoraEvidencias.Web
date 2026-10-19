# Corte productivo de 0.2.0

19/10/2026. **SQL activo y aceptado funcionalmente; tareas SQL habilitadas.** El responsable autorizo la ventana sin usuarios y confirmo `https://192.168.1.95:5067`. El certificado existente cubre esa IP y vence el 28/07/2029. Sigue pendiente completar CHECKDB de la copia restaurada por presion de memoria del equipo; no declarar recuperacion integral certificada.

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

## Limites pendientes

- El BAK del primer ZIP productivo se restauro en base temporal nueva. CHECKDB no termino en dos intentos (normal y MAXDOP=1): espera RESOURCE_SEMAPHORE y timeout; no se obtuvo un resultado de integridad. Se eliminaron solo las bases temporales con marcador de propiedad comprobado. SQL reporto unos 700 MB libres de 8 GB, memoria fisica baja y unos 5.62 MB disponibles para concesiones de consultas. No se cambiaron parametros globales de SQL ni se cerraron programas del usuario; se apagaron servidores de compilacion propios. El responsable ofrecio liberar RAM y avisar para completar esta prueba. No confundir VERIFYONLY aprobado con CHECKDB aprobado.
- Conservar alerta de espacio y revisar capacidad sin borrar respaldos automaticamente.
- Completar aceptacion visual especifica de validaciones/confirmaciones de formularios y recuperacion integral en copia. El acceso productivo y las consultas ya fueron confirmados.
- Custodiar secretos por un mecanismo recuperable externo. Los archivos DPAPI preparados sirven para la misma identidad Windows y equipo; no sustituyen un respaldo portable para otro equipo.
- No crear v0.2.0 como cierre de todas las comprobaciones mientras esta pendiente la revision de integridad; la version declarada de la aplicacion desplegada es 0.2.0.

Las rutas exactas operativas y el avance tecnico se guardan localmente en `tmp/production-cutover-state.json` y `tmp/production-deployment-state.json`, excluidos de Git. No ejecutar import otra vez: SQL ya contiene el corte verificado. No volver a SQLite sin revisar primero si SQL admitio nuevas escrituras.
