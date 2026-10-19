# Estado de aceptacion de 0.2.0

Corte documental: **19/10/2026**. [SQL activo en produccion](CORTE-PRODUCTIVO-0.2.0.md): SQLite detenida, respaldo final creado, SQL importado/verificado, ingreso/consultas aceptados y tareas SQL habilitadas. CHECKDB de la copia restaurada sigue pendiente por presion de RAM; no se ha creado v0.2.0. Las evidencias de ensayo siguientes se distinguen del corte actual en ese registro.

## Conservacion y entorno

- SQLite 0.1.0 congelada en etiqueta v0.1.0 y rama archive/sqlite-0.1, referencia 6aba98e.
- Desarrollo SQL en feature/sqlserver-0.2; instancia .\Auditoria.
- Instalacion productiva SQL C:\BitacoraEvidencias, https://192.168.1.95:5067; SQLite conservada y detenida.
- Copias/ensayo C:\BitacoraEvidencias_Pruebas, base BitacoraEvidencias_Pruebas, puerto 5068. La base ya tiene importacion y escrituras de ensayo; no esta vacia.
- BitacoraEvidencias productiva fue comprobada vacia antes del corte y ahora contiene la importacion final verificada. No repetir import ni borrar/recrear.
- Publicacion de ensayo mas reciente comprobada: App-backup-final. Incluye correcciones de consultas/formularios y control local. Los scripts operativos se ejecutan desde el repositorio actualizado.

## Evidencia disponible

| Area | Resultado y limite |
| --- | --- |
| Importacion inicial | Transaccion y verify aprobados: 6 usuarios, 824 oficios, 893 casos, 1,828 evidencias y 1,904 eventos del respaldo inicial. Datos posteriores de ensayo no deben compararse como si fueran ese corte. |
| Panel y navegacion | Corregidas consultas concurrentes del DbContext; el responsable confirmo funcionamiento del ensayo. |
| Busquedas | Espera SQL reproducida y corregida; mediciones de handler 347/27 ms tras unos 25 s anteriores. El responsable confirmo solucion y funcionamiento. |
| Formularios | Implementados y probados con reglas acordadas; 30 comprobaciones DOM. Falta aceptacion visual completa de esta mejora en navegador. |
| Suite .NET | Ultima completa Debug: 132 aprobadas, cero fallos/omitidas. Fixtures de almacenamiento aislados de la clave real, cobertura de rutas antiguas/protegidas y bootstrap SQLite separado de las migraciones congeladas. Ver TESTING.md para alcance y limites. |
| Respaldo/restauracion | BAK/VERIFYONLY y ZIP SHA256 probados. Restauracion en base temporal y DBCC CHECKDB sin incidencias; cinco tablas con conteos iguales. No equivale a aceptar toda la aplicacion recuperada. |
| Historicos | Exportadas 1,900 filas reales de ensayo sin retirarlas. Borrado transaccional/reversion comprobados con datos sinteticos en base temporal. |
| Respaldo con historicos | 3,656 archivos de evidencias y 20 historicos/manifiestos incluidos y comparados. No se mezclan con la cifra inicial de 3,658 archivos totales extraidos. |
| Operacion | 39 comprobaciones de backup y 31 de operaciones con integracion. Monitor real HTTP/SQL/ZIP y reinicio ante fallo ensayados en instancia aislada. |
| Tareas | SQL habilitadas: monitor 09:15, archivo 09:20, backup 14:00 de Mexico. Backup real con cuenta exclusiva y cierre/reinicio aprobo, resultado 0/Ready. SQLite conservadas y deshabilitadas. |

## Pendientes despues del corte

1. Aceptar visualmente formularios, confirmaciones y edicion de historicos incompletos, incluidos flujos HTMX.
2. Completar revision de dependencias. Las seis fallas anteriores se atendieron adaptando las pruebas y la suite completa aprobo 132 casos. Ambos recorridos SQL sinteticos aprobaron anteriormente 47 comprobaciones, con un SKIP de timeout cada uno.
3. Completar CHECKDB de la copia restaurada al liberar RAM y recuperacion integral en copia: usuarios/roles, datos cifrados, originales, miniaturas y escrituras controladas. El BAK del corte se restauro, pero la comprobacion de integridad no termino por RESOURCE_SEMAPHORE; no declararla aprobada.
4. Revisar capacidad de RAM/disco y custodiar secretos mediante un mecanismo externo recuperable; DPAPI solo permite la misma identidad/equipo. No borrar respaldos automaticamente ni modificar limites para ocultar alertas.

El monitor del corte encontro 20.15 GB libres en C frente al umbral de 25 GB; los respaldos posteriores tambien consumen espacio. Las tareas requieren sesion Windows abierta; se confirmo que permanecera abierta a las 14:00. Credenciales SQL de aplicacion/operaciones y backup DPAPI, permisos de staging, arranque instalado, acceso manual y ciclo real de reinicio ya quedaron preparados y probados; ver el registro del corte.

Volver a SQLite antes de admitir escrituras puede recuperar el corte consistente. Despues de escrituras nuevas en SQL exige conciliacion; Git no transfiere registros ni secretos. Ver [MIGRACION-0.2.0.md](MIGRACION-0.2.0.md).
