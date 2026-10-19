# Pendientes operativos y de seguridad de 0.2.0

Revision documental: 19/10/2026. El cierre de controles de SQLite en febrero/mayo no constituye una comprobacion SQL. Las actas anteriores se conservan en el historial 0.1.0; no se certifica cumplimiento normativo mediante este archivo.

## Pendientes

| Control | Trabajo necesario |
| --- | --- |
| Aceptacion funcional | Formularios/confirmaciones, historicos incompletos y flujos HTMX en navegador. |
| Pruebas | Revisar dependencias. Suite completa Debug: 132 aprobadas, cero fallos/omitidas tras adaptar fixtures y alcance SQLite. Payloads smoke: 47 PASS/1 SKIP cada variante sobre SQL sintetico. |
| Privilegios SQL | Comprobar cuenta de operacion y separar permisos de migracion/backup/archivo. |
| TLS | Confianza SQL y HTTPS del despliegue final, sin confundir excepcion local con validacion completa. |
| ACL | Comprobar evidencias/historicos, carpeta BAK y credenciales con la identidad real de las tareas. |
| Secretos | Custodia recuperable de llave, certificado y credenciales; recrear DPAPI al trasladar cuenta/equipo. |
| Automatizacion | Preparar credenciales y registrar tareas SQL deshabilitadas; activar solo en el corte. |
| Recuperacion | Completar ensayo integral de usuarios, datos cifrados y fotos con copia restaurada. |
| Arranque productivo | Lanzador SQL independiente preparado; ensayar cuenta/permisos finales y cambio de acceso directo manteniendo SQLite disponible. |
| Capacidad | Revisar espacio: ultima medicion unos 23 GB libres frente a umbral de 25 GB. |
| Publicacion | Revisar documentos internos/historial antes de compartir; no subir evidencias operativas. |

## Evidencia ya disponible

Importacion/verify aprobados, correccion de rendimiento confirmada por el responsable, BAK restaurado con CHECKDB sin incidencias, exportacion sin retirada de registros reales de ensayo, pruebas transaccionales sobre datos sinteticos y monitor/reinicio en instancia aislada. No se modificaron las tareas productivas ni se migro produccion.

El detalle vigente y sus limites se mantienen en [ESTADO-0.2.0.md](ESTADO-0.2.0.md). Politicas y horarios en [OPERATIONS.md](OPERATIONS.md). No marcar un control como cerrado solo por existir su script o esta documentacion.
