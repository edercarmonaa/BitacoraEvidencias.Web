# Estado de aceptacion de 0.2.0

Corte documental: **19/10/2026**. Describe las evidencias de esta sesion; no es una nueva inspeccion de produccion. SQL Server 0.2.0 continua en ensayo. No se ha autorizado ni ejecutado su sustitucion productiva ni creado v0.2.0.

## Conservacion y entorno

- SQLite 0.1.0 congelada en etiqueta v0.1.0 y rama archive/sqlite-0.1, referencia 6aba98e.
- Desarrollo SQL en feature/sqlserver-0.2; instancia .\Auditoria.
- Instalacion productiva SQLite C:\BitacoraEvidencias, puerto 5067.
- Copias/ensayo C:\BitacoraEvidencias_Pruebas, base BitacoraEvidencias_Pruebas, puerto 5068. La base ya tiene importacion y escrituras de ensayo; no esta vacia.
- BitacoraEvidencias productiva fue confirmada existente/vacia durante la preparacion. Comprobar de nuevo antes del corte; no borrarla/recrearla.
- Publicacion de ensayo mas reciente comprobada: App-backup-final. Incluye correcciones de consultas/formularios y control local. Los scripts operativos se ejecutan desde el repositorio actualizado.

## Evidencia disponible

| Area | Resultado y limite |
| --- | --- |
| Importacion inicial | Transaccion y verify aprobados: 6 usuarios, 824 oficios, 893 casos, 1,828 evidencias y 1,904 eventos del respaldo inicial. Datos posteriores de ensayo no deben compararse como si fueran ese corte. |
| Panel y navegacion | Corregidas consultas concurrentes del DbContext; el responsable confirmo funcionamiento del ensayo. |
| Busquedas | Espera SQL reproducida y corregida; mediciones de handler 347/27 ms tras unos 25 s anteriores. El responsable confirmo solucion y funcionamiento. |
| Formularios | Implementados y probados con reglas acordadas; 30 comprobaciones DOM. Falta aceptacion visual completa de esta mejora en navegador. |
| Suite .NET | Ultima completa: 115 pruebas, 109 aprobadas/seis fallos previos. Despues pasaron seis pruebas dirigidas de estado de reinicio, incluida una nueva; no se repitio toda la suite. |
| Respaldo/restauracion | BAK/VERIFYONLY y ZIP SHA256 probados. Restauracion en base temporal y DBCC CHECKDB sin incidencias; cinco tablas con conteos iguales. No equivale a aceptar toda la aplicacion recuperada. |
| Historicos | Exportadas 1,900 filas reales de ensayo sin retirarlas. Borrado transaccional/reversion comprobados con datos sinteticos en base temporal. |
| Respaldo con historicos | 3,656 archivos de evidencias y 20 historicos/manifiestos incluidos y comparados. No se mezclan con la cifra inicial de 3,658 archivos totales extraidos. |
| Operacion | 39 comprobaciones de backup y 31 de operaciones con integracion. Monitor real HTTP/SQL/ZIP y reinicio ante fallo ensayados en instancia aislada. |
| Tareas | Horarios confirmados: monitor 09:15, archivo 09:20, backup 14:00 de Mexico. Registradores listos; tareas SQL no registradas/activadas. Tareas SQLite intactas. |

## Pendientes antes del cambio

1. Aceptar visualmente formularios, confirmaciones y edicion de historicos incompletos, incluidos flujos HTMX.
2. Investigar los seis fallos conocidos y repetir la suite completa. Los payloads smoke ya fueron actualizados y ambos recorridos SQL sinteticos aprobaron 47 comprobaciones, con un SKIP de timeout cada uno. Completar revision de dependencias antes de liberar.
3. Verificar conexion/permisos con el usuario SQL de operacion, separado de la cuenta administradora usada en ensayos. Resolver confianza TLS SQL/HTTPS del entorno final.
4. Preparar localmente credenciales DPAPI de backup/operaciones, carpetas/permisos y ensayo con esas credenciales. No solicitar contrasenas en el chat.
5. Completar recuperacion integral: usuarios/roles, datos cifrados, originales, miniaturas y escrituras controladas en copia restaurada; la prueba SQL ya realizada no cubre todo ese flujo.
6. Ensayar el arranque SQL con la cuenta/permisos finales y el cambio de acceso directo. Ya existen publicador de release y lanzador SQL independientes, probados con publicacion local y validacion sin conectar/iniciar. Conservar binarios/configuracion SQLite.
7. Acordar mantenimiento/responsable, crear copia final reciente y consistente con historicos/secretos bajo custodia y validar destino SQL vacio antes de migrar.
8. En el cambio aprobado, activar tareas SQL y desactivar SQLite. Verificar cierre/respaldo/reinicio y monitoreo con configuracion productiva antes de reabrir usuarios.

El ultimo monitor encontro unos 23 GB libres en C frente al umbral de 25 GB. Revisar capacidad sin borrar datos o respaldos automaticamente. Las tareas requieren sesion Windows abierta; se confirmo que permanecera abierta a las 14:00.

Volver a SQLite antes de admitir escrituras puede recuperar el corte consistente. Despues de escrituras nuevas en SQL exige conciliacion; Git no transfiere registros ni secretos. Ver [MIGRACION-0.2.0.md](MIGRACION-0.2.0.md).
