# Resumenes incrementales de Reportes

Estado: desplegado y habilitado en produccion el 20/10/2026, despues de la aceptacion del ensayo y de la ventana de mantenimiento autorizada. Las etiquetas publicadas y la rama SQLite se conservan.

## Despliegue productivo del 20/10/2026

- URL vigente: `https://192.168.1.95:5067/Login`. Login HTTP 200 con TLS valido; Reportes sin sesion devuelve HTTP 302 hacia autenticacion. La comprobacion visual del usuario en produccion queda pendiente.
- Publicacion activa: `C:\BitacoraEvidencias\deploy-sql\releases\0.2.0_reports_20261020_fb3e703c65b2445cad878c85053ff57c`, commit de aplicacion `f962f387c8f4cb76df43469b701bba67b57e3cc7`. Los 291 archivos copiados coinciden por SHA256 con la publicacion aceptada en ensayo. No se movio ni creo una etiqueta de lanzamiento.
- Nueva copia de scripts operativos: `C:\BitacoraEvidencias\deploy-sql\operations\0.2.0_reports_20261020_fb3e703c65b2445cad878c85053ff57c`. Incluye la correccion del lanzador instalado para leer `ReportSummariesEnabled` del manifiesto; ValidateOnly y el arranque real aprobaron. El manifiesto identifica la base de scripts `3ca1d5b` y la modificacion local de ese lanzador mediante SHA256.
- `active.json` conserva la misma IP, puerto, evidencias y archivos DPAPI de configuracion/credencial; agrega `ReportSummariesEnabled=true`. El acceso manual y las tres tareas SQL apuntan a los nuevos scripts. Se conservaron credenciales, principal, horarios y estado habilitado: monitor 09:15, archivado 09:20 y respaldo 14:00 de Mexico. Proximo backup del dia: 20/10/2026 a las 14:00.
- La aplicacion se detuvo ordenadamente antes del respaldo y del cambio SQL. Respaldo completo previo: `BitacoraSqlBackup_BitacoraEvidencias_20261020_110538_83983de32b7e480f9123ade431990db0.zip`, bajo `C:\BitacoraEvidencias\BackupsBitacora\SqlServer\BitacoraEvidencias`. SHA256 `1B64F3031A2EF71C8FAE0B17908ED78ABAD90E49283BD91B737F39631AECEEE9`. COPY_ONLY/CHECKSUM/VERIFYONLY aprobados, 3656 evidencias comparadas por SHA256 y 44 archivos historicos incluidos. No se restauro este ZIP ni se ejecuto CHECKDB sobre una restauracion nueva durante esta actualizacion.
- `ReportSummaries` aplicada; reconstruccion en 33 lotes; comparacion completa de resumenes contra origen aprobada. Huellas de todas las filas y columnas de las cinco tablas originales identicas antes/despues: 6 usuarios, 824 oficios, 893 casos, 1828 fotografias y 19 eventos SQL. La cifra de auditoria corresponde a la retencion vigente, con historicos conservados; no al corte inicial del 19/10.
- Handlers con datos productivos y llave existente: Reportes 1399 ms en primera carga, siguientes 82/86 ms; Ejecutivo 182 ms y siguientes 86/60 ms, periodo 2025-2026. Mediciones del handler sin renderizado HTTP/navegador. Se leyeron y descifraron las filas requeridas sin error.
- Nueva instancia verificada por el canal local: carpeta productiva nueva, base BitacoraEvidencias, IP/5067, resumenes activos y cola vacia. Las tareas SQL se rehabilitaron tras comprobar el arranque. El monitor actualizado devolvio codigo 0, todos los controles OK; 27.17 GB libres frente al umbral conservado de 25 GB.
- Custodia de manifiesto, acceso manual y XML de las tareas anteriores: `C:\BitacoraEvidencias\deploy-sql\changes\reports_20261020_fb3e703c65b2445cad878c85053ff57c`. La publicacion y los scripts anteriores permanecen en sus carpetas. Los secretos no se copiaron al repositorio.

Para revertir, detener primero las escrituras y conservar un respaldo actualizado. El binario anterior no debe escribir sobre las tablas con los triggers de reportes habilitados: se necesita retirar expresamente esa migracion derivada mediante un procedimiento validado, o una recuperacion planificada. Restaurar el ZIP previo sin contemplar capturas posteriores perderia esas capturas. No basta con restaurar active.json o cambiar la bandera. Las tablas originales no se modificaron durante esta migracion.

## Despliegue de ensayo del 20/10/2026

- Codigo publicado: commit `f962f38`, rama `feature/report-summaries`.
- Aplicacion: `C:\BitacoraEvidencias_Pruebas\App-report-summaries-f962f38`; URL `https://192.168.1.95:5068/Login`, IP cubierta por el certificado existente. No usar 127.0.0.1 con ese certificado.
- Base: `.\Auditoria`, `BitacoraEvidencias_Pruebas`. Migracion ReportSummaries aplicada, reconstruccion en 33 lotes y reports-verify aprobado para todas las filas/dimensiones.
- Datos de ensayo: 824 oficios, 893 casos y 1828 fotografias. Copia independiente de evidencias/historicos; 3656 originales/miniaturas referenciados verificados SHA256, sin faltantes ni diferencias.
- Respaldo previo de la base de pruebas: `BitacoraSqlBackup_BitacoraEvidencias_Pruebas_20261020_104931_ad4f4e2ae9964ff9b699afe9f90acbe9.zip`, bajo `C:\BitacoraEvidencias_Pruebas\Backups\SqlServer\BitacoraEvidencias_Pruebas`; COPY_ONLY, CHECKSUM y VERIFYONLY aprobados.
- Handlers con resumenes y llave existente: primer Reportes 1360 ms, siguientes 99/69 ms; Ejecutivo 181 ms y siguientes 54/91 ms, periodo 2025-2026. Mediciones directas del handler, sin navegador/renderizado HTTP. Las consultas paginadas descifraron las filas requeridas sin error.
- Login de ensayo: HTTPS valido y HTTP 200; Reportes sin sesion: HTTP 302 hacia autenticacion. Falta aceptacion funcional con usuario habitual en el navegador.
- Ciclo real de respaldo completo y reinicio aprobado en ensayo: SQL COPY_ONLY/CHECKSUM/VERIFYONLY, 3656 archivos de evidencia comparados SHA256 y 24 historicos incluidos. ZIP `BitacoraSqlBackup_BitacoraEvidencias_Pruebas_20261020_105547_ccc4bfce5a7f4a9c9c6788872204ad4a.zip` en la carpeta de respaldos de pruebas. No se realizo restauracion de este ZIP en este despliegue.
- Tras el reinicio se comprobaron por el canal local la misma base/carpeta de ensayo, `Reports__SummariesEnabled=true`, resumenes listos y cola vacia. Login volvio a responder HTTP 200 con TLS valido. El manifiesto local sin secretos `C:\BitacoraEvidencias_Pruebas\report-summaries-trial.json` identifica el proceso activo; no es el manifiesto productivo.
- Durante el ensayo, produccion se conservo sin cambios en el puerto 5067. Posteriormente se actualizo en la ventana productiva documentada arriba.

## Funcionamiento

`ReportOffices` contiene primera fecha de verificacion e indicadores de auditoria por oficio. `ReportCases` contiene dimensiones, fechas, cantidad de fotos e indicadores de enlace/evidencia por caso. No almacena nombres, CURP, secretos, enlaces ni rutas de archivos. El numero de oficio se conserva para reproducir el conteo ejecutivo de numeros distintos despues de Trim; otros reportes cuentan OficioId distintos. No deben intercambiarse estas reglas.

Tres triggers ligeros agregan notificaciones a `ReportChangeQueue` en la misma transaccion que las altas, modificaciones y bajas de Oficios, CasosCorreccion y EvidenciasFoto, incluyendo cascadas y cambios SQL ordinarios. No calculan reportes durante la captura. La cola es append-only: varios eventos del mismo oficio se consolidan al procesarlos. Importaciones mediante SqlBulkCopy deben habilitar FireTriggers si ya estan instalados los resumenes; cargas que deshabiliten triggers, TRUNCATE, restauraciones o modificaciones manuales que los omitan requieren reconstruccion. El importador SQLite instala solo el esquema original y los indices; los objetos de reportes se agregan despues con reports-upgrade.

Un BackgroundService procesa al arrancar y luego cada minuto **un solo lote de hasta 25 oficios**. Usa contexto propio, bloqueo SQL transaccional para impedir trabajadores superpuestos, prioridad baja frente a interbloqueos y espera de bloqueos limitada a un segundo. El lote publica sus cambios y elimina solo los eventos capturados dentro de una unica transaccion. Eventos posteriores permanecen pendientes. Ante error, se conserva el trabajo para el siguiente intento. No abre fotografias ni accede al almacenamiento.

El minuto es intervalo de comprobacion, no garantia de antiguedad maxima: importaciones grandes requieren varios lotes y el proceso depende de que la aplicacion este encendida. No se ejecuta una reconstruccion completa en cada minuto. No hay dependencia de SQL Server Agent.

`ReportRefreshState.Ready` se activa al terminar la carga inicial. Hasta entonces se consultan los datos originales. Una vez listo se mantienen las lecturas del resumen durante los reintentos. UpdatedAtUtc indica la ultima vez que la cola quedo vacia; se muestra en horario de Ciudad de Mexico. Durante una acumulacion de pendientes puede haber oficios de diferentes lotes. Los distintos comandos de una pagina no constituyen una instantanea transaccional global. Los detalles operativos consultan registros actuales; pueden diferir temporalmente de los contadores mientras llega el siguiente lote.

El reporte principal cuenta fotografias; el ejecutivo suma fotografias y enlaces de los casos verificados en el periodo. Un oficio se asigna a su primera verificacion, aunque tenga casos en otro mes/anio. Oficios sin casos se incluyen en el total general. El listado de pendientes sigue paginado y el acceso a datos personales queda limitado a las filas mostradas.

## Ensayo y activacion

1. Compilar el codigo y el ejecutable de mantenimiento:

   ```powershell
   dotnet build tools/BitacoraMigration/BitacoraMigration.csproj -c Release
   ```

2. Usar una base de ensayo existente con el esquema 0.2.0 y una copia representativa. Detener las escrituras y cualquier trabajador de reportes durante instalacion/reconstruccion/verificacion. Conservar un respaldo SQL verificado.

3. Ejecutar, por separado y revisando cada resultado:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\migration\Invoke-BitacoraSqlMigration.ps1 -Mode reports-upgrade -TrustLocalSqlCertificate
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\migration\Invoke-BitacoraSqlMigration.ps1 -Mode reports-rebuild -TrustLocalSqlCertificate
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\migration\Invoke-BitacoraSqlMigration.ps1 -Mode reports-verify -TrustLocalSqlCertificate
   ```

   El destino predeterminado es BitacoraEvidencias_Pruebas en .\Auditoria. Las credenciales se piden de forma interactiva. `-TrustLocalSqlCertificate` es solo para el certificado SQL local conocido. Los modos de reportes no necesitan los archivos SQLite de entrada. El modo antiguo `upgrade` sigue instalando solo indices, nunca reportes.

   `reports-rebuild` vuelve a encolar los oficios actuales y los identificadores antiguos del resumen, para retirar tambien filas huerfanas. No elimina datos originales. Procesa lotes consecutivos para completar la carga inicial sin esperar un minuto entre ellos. `reports-verify` compara todas las filas y dimensiones del resumen con el origen bajo una transaccion de lectura serializable; exige cola vacia e inicializacion completa. Esta comprobacion completa es para mantenimiento, no para cada visita a Reportes.

4. Configurar `Reports__SummariesEnabled=true` en el entorno del proceso de ensayo y arrancar **el binario nuevo**. Los archivos .env no se cargan automaticamente. Comparar general, ejecutivo, tipos, filtros diarios y paginacion; modificar un caso y confirmar la actualizacion tras el siguiente lote. Registrar tiempos de handler/SQL, consumo de memoria y tiempo de captura antes/despues, con la misma carga.

5. Solo despues de aceptar el ensayo, planificar una nueva publicacion y repetir respaldo/instalacion/reconstruccion/verificacion para produccion con `-Production`. El instalador requiere confirmacion de mantenimiento y no cambia la instalacion activa por si mismo. Conservar IP/puerto productivos 192.168.1.95:5067. No reutilizar la etiqueta v0.2.0 para este codigo.

   Actualizar tambien la copia instalada del script `Invoke-BitacoraScheduledSqlBackup.ps1`: el control de respaldo y su lista de variables de reinicio ahora conservan `Reports__SummariesEnabled`. No combinar el binario nuevo con el script instalado anterior, que rechaza ese prefijo. Verificar un ciclo de respaldo/reinicio en ensayo antes de publicar.

**Compatibilidad con triggers:** EF SQL Server deshabilita OUTPUT sin INTO en las tres tablas de origen. El acceso EF del binario nuevo es compatible con los triggers; su validacion de arranque exige tener aplicada la migracion nueva, aun si la lectura de resumenes esta deshabilitada. El binario 0.2.0 anterior no debe escribir con estos triggers instalados. Durante el cambio detener la version anterior antes de reports-upgrade. Para una reversion a 0.2.0 se necesita retirar primero la migracion de reportes en mantenimiento, o restaurar el respaldo previo considerando las escrituras posteriores. Deshabilitar solo Reports__SummariesEnabled no elimina triggers.

Con `Reports__SummariesEnabled=false`, el nuevo binario consulta el origen y el trabajador no se inicia, pero los triggers ya instalados continuan encolando cambios. Vigilar el crecimiento de la cola si se deja asi; reconstruir/verificar antes de rehabilitar resumenes. El respaldo SQL incluye tanto originales como cola y resumenes. Tras restaurar una copia consistente puede continuar la cola; ante dudas reconstruir y verificar.

## Pruebas

La suite ordinaria prueba el camino directo con SQLite y la separacion de migraciones. La prueba SQL se habilita solo mediante una conexion master local de prueba; crea `Bitacora_ReportTest_<GUID>` con datos sinteticos y elimina exclusivamente esa base al terminar. No utiliza ni modifica BitacoraEvidencias o BitacoraEvidencias_Pruebas.

```powershell
$env:BITACORA_REPORT_TEST_SQL='Server=.\Auditoria;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true'
try {
    dotnet test tests/BitacoraEvidencias.Web.Tests/BitacoraEvidencias.Web.Tests.csproj --logger 'console;verbosity=detailed'
} finally {
    Remove-Item Env:BITACORA_REPORT_TEST_SQL
}
```

Se comprueban migracion, escrituras EF con triggers, carga parcial, oficios vacios, fechas en distintos ejercicios, fotos/enlaces, cambios historicos, eliminacion en cascada, rollback del origen, fallo/reintento del trabajador, eventos posteriores al watermark y equivalencia de los reportes. Se imprimen mediciones sinteticas de 824 oficios; no son una promesa de tiempos productivos. Se requiere una cuenta Windows de pruebas que pueda crear/eliminar esa base temporal. No aumentar privilegios de la cuenta de la aplicacion para estas pruebas.

Resultado del 20/10/2026: 138 pruebas aprobadas, cero fallos y cero omisiones, incluida la integracion SQL con cifrado y llave sinteticos. Se corrigio la comparacion de enlaces contra cadena vacia: el convertidor EF la transformaba en NULL y podia excluir enlaces existentes. Las consultas de reportes ahora comprueban longitud del valor almacenado sin descifrarlo.

En la ultima ejecucion, con 824 oficios sinteticos y despues de calentar la traduccion de consultas, cinco lecturas del reporte general promediaron 269.6 ms en modo directo y 203.2 ms con resumenes (aproximadamente 25% menos). La reconstruccion por lotes duro 2243 ms. Es una medicion local con carga sintetica, frente al camino directo del mismo codigo optimizado; no compara dos despliegues productivos ni incluye renderizado/navegador. No extrapolar el porcentaje a otros reportes o volumenes. La suite tambien encontro esperas RESOURCE_SEMAPHORE en consultas directas; el uso de resumenes no sustituye verificar la memoria disponible del servidor.
