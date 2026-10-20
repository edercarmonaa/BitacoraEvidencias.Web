# Resumenes incrementales de Reportes

Estado: desarrollo del 20/10/2026, posterior a la publicacion de 0.2.0. No instalado ni habilitado en produccion. Las etiquetas publicadas y la rama SQLite se conservan.

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
