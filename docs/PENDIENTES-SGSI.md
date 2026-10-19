# Pendientes operativos y de seguridad de 0.2.0

Revision documental: 19/10/2026. El cierre de controles de SQLite en febrero/mayo no constituye una comprobacion SQL. Las actas anteriores se conservan en el historial 0.1.0; no se certifica cumplimiento normativo mediante este archivo.

## Pendientes

| Control | Trabajo necesario |
| --- | --- |
| Aceptacion funcional | Formularios/confirmaciones, historicos incompletos y flujos HTMX en navegador. |
| Pruebas | Revisar dependencias. Suite completa Debug: 132 aprobadas, cero fallos/omitidas tras adaptar fixtures y alcance SQLite. Payloads smoke: 47 PASS/1 SKIP cada variante sobre SQL sintetico. |
| Privilegios SQL | Cuenta de operacion probada; BitacoraRespaldo tiene BACKUP DATABASE y CREATE ANY DATABASE expresamente autorizados, sin sysadmin. Revisar posteriormente el minimo privilegio de operacion, que conserva su db_owner preparado para migracion. |
| TLS | Confianza SQL y HTTPS del despliegue final, sin confundir excepcion local con validacion completa. |
| ACL | Staging y credenciales privados preparados; ciclo real de backup con la identidad de las tareas aprobado. Revalidar al cambiar cuenta/rutas. |
| Secretos | Custodia recuperable de llave, certificado y credenciales; recrear DPAPI al trasladar cuenta/equipo. |
| Automatizacion | SQL habilitadas tras el corte; SQLite deshabilitadas. Backup real con cierre/reinicio resultado 0. Verificar siguientes ejecuciones programadas. |
| Recuperacion | CHECKDB del BAK productivo restaurado y cinco conteos aprobados; dos timeouts previos por memoria quedan como antecedente. Completar ensayo integral de usuarios, datos cifrados y fotos en copia. |
| Arranque productivo | SQL activo en IP/5067; ingreso/consultas aceptados, acceso manual actualizado y reinicio tras backup con la misma llave. SQLite conservada y detenida. |
| Capacidad | Monitor del corte: 20.15 GB libres frente a umbral 25 GB antes del segundo ZIP. Revisar presion de RAM: habia unos 700 MB libres de 8 GB; al completar CHECKDB habia unos 1,050 MB y SQL ya no reportaba memoria baja. |
| Publicacion | Revisar documentos internos/historial antes de compartir; no subir evidencias operativas. |

## Evidencia ya disponible

Importacion/verify aprobados, correccion de rendimiento confirmada por el responsable, BAK restaurado con CHECKDB sin incidencias, exportacion sin retirada de registros reales de ensayo, pruebas transaccionales sobre datos sinteticos y monitor/reinicio en instancia aislada. No se modificaron las tareas productivas ni se migro produccion.

El detalle vigente y sus limites se mantienen en [ESTADO-0.2.0.md](ESTADO-0.2.0.md). Politicas y horarios en [OPERATIONS.md](OPERATIONS.md). No marcar un control como cerrado solo por existir su script o esta documentacion.
