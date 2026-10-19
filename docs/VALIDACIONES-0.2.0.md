# Validaciones y mensajes de formularios de 0.2.0

Reglas acordadas e implementacion para ensayo de 0.2.0. La migracion productiva queda pendiente de validar esta mejora en navegador.

## Interaccion acordada

- Validar al salir de cada campo y nuevamente al enviar el formulario.
- Mostrar errores junto al campo correspondiente y un resumen en toast.
- Usar ventanas de confirmacion para acciones que requieren confirmacion, como eliminar.
- Conservar los valores capturados cuando haya errores.
- Revisar los formularios de la aplicacion y reforzar sus reglas; mantener validacion de servidor, ademas de la del navegador.

## Reglas confirmadas

| Campo o relacion | Regla |
| --- | --- |
| Numero de oficio | Obligatorio y unico en todo el sistema, sin distinguir mayusculas y minusculas: OF-001 y of-001 son el mismo numero. Quitar espacios exteriores. |
| Fecha del oficio | No permite fechas futuras. |
| Casos por oficio | Puede tener cero, uno o dos casos: maximo uno de Primaria y uno de Secundaria. Se permite eliminar el ultimo caso. |
| CURP | Obligatoria. Validar formato, sin consultar padron oficial. Se permite repetirla en distintos casos y oficios, respetando el limite por nivel. |
| CCT | Obligatoria. Validar formato, sin consultar padron oficial. Aceptar claves estandar de 10 caracteres, como 30PBH0442H y 30PBH0260Z, y claves antiguas de tres digitos, guion y dos letras o numeros, como 184-D2 y 327-DV. El usuario confirma que estos cubren los formatos existentes. Quitar espacios exteriores. |
| Nombre completo | Obligatorio. |
| Folio | Opcional. Correccion expresa del usuario sobre la respuesta inicial. |
| Validador | Opcional. |
| Fecha de verificacion | Obligatoria, igual o posterior a la fecha del oficio y sin fechas futuras. |
| Periodo de inicio y periodo de fin | Ambos obligatorios. El fin no debe ser anterior al inicio. |

## Implementacion

Se reutilizan jQuery Validation/unobtrusive y Bootstrap Toast/Modal, ya incluidos localmente. No se incorporan CDN ni dependencias nuevas para la aplicacion. Los scripts se cargan una vez desde el layout y las reglas se vuelven a conectar al sustituir contenido con HTMX.

En formularios POST se valida al salir del campo y al enviar. Los errores permanecen al lado del campo; al intentar enviar se muestra un resumen en toast con hasta cinco mensajes y aviso de errores adicionales. Los toast de error permanecen hasta cerrarlos. Los avisos de exito duran siete segundos; los de administracion de usuarios permanecen hasta cerrarlos para poder leer las credenciales temporales generadas. Los mensajes usan texto, no HTML interpretado. Sin JavaScript se conservan los errores y resumen de servidor.

Las confirmaciones se aplican a eliminar oficio/caso/evidencia, importar registros, restablecer contrasena, cambiar rol y activar/desactivar usuario. Cancelar no envia el POST. Guardar conserva el submitter y la integracion HTMX. No se agregan confirmaciones a cada guardado ordinario.

Las reglas nuevas de casos se aplican en altas, ediciones e importaciones; las fechas usan el dia de Ciudad de Mexico. CURP/CCT nuevas o modificadas se normalizan a mayusculas. El formato CURP incluye fecha de calendario valida y codigo de entidad; no se verifica existencia en padron ni digito de control calculado.

La unicidad del oficio se comprueba al salir del campo y al guardar. Altas, cambios e importaciones de oficios serializan lectura/comprobacion/escritura con transacciones para proteger las rutas de aplicacion ante concurrencia. No se cambia la intercalacion de la base ni se agrega una migracion de esquema. Los registros historicos no se renumeran. Comprobacion de solo lectura en la base de ensayo: cero grupos de oficios repetidos al ignorar mayusculas.

Los adjuntos conservan tipos y limites existentes: fotografias JPG/JPEG/PNG/BMP/GIF/TIF/TIFF, con limite por archivo tomado de Storage; importaciones CSV/XLSX hasta 10 MB y 1000 filas. El navegador anticipa errores de seleccion, extension y tamano; el servidor mantiene las comprobaciones de contenido, dimensiones y estructura del archivo. Los enlaces opcionales de evidencia aceptan solo HTTP/HTTPS. Las reglas existentes de contrasenas, roles y catalogos se conservan.

Los filtros de consulta siguen siendo opcionales y conservan busqueda de datos historicos; no se les aplican los campos obligatorios de alta de casos. Tampoco se alteran los limites y consultas de paginacion ya optimizados.

La nueva validacion no debe eliminar, completar ni corregir automaticamente registros existentes durante la migracion. El usuario confirma que se permite conservar datos historicos incompletos. En altas se aplican las reglas nuevas; al editar se conservan campos historicos incompletos sin cambios y se validan los campos modificados. Las restricciones de unicidad y nivel por oficio se mantienen.

La comparacion de valores historicos se hace con el registro persistido, no con valores originales enviados por el navegador. Cambiar un dato valido por vacio queda rechazado. Cambiar la fecha del oficio tambien comprueba que no quede despues de las verificaciones de sus casos. No se escribieron registros de negocio durante la preparacion de esta mejora.

## Comprobacion del ensayo

Publicacion inicial de esta mejora: `App-validation-fix` (historica). Para verificar el conjunto actual usar `C:\BitacoraEvidencias_Pruebas\App-backup-final`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\deploy\Start-BitacoraSqlServer.ps1 -AppFolder App-backup-final
```

Detener primero el ensayo anterior del puerto 5068. En navegador comprobar: campo vacio al salir, resumen al guardar con errores, las cuatro CCT de ejemplo, fechas y periodos incorrectos, repeticion de numero cambiando mayusculas, nivel ya ocupado, edicion de historico incompleto, adjuntos incorrectos, y aceptar/cancelar confirmaciones. Probar tambien entrar a formularios mediante navegacion HTMX. Los valores de texto se conservan al fallar; por restricciones del navegador puede ser necesario seleccionar nuevamente archivos despues de una respuesta del servidor. Las contrasenas conservan su tratamiento de seguridad existente.

Las pruebas de JavaScript usan un DOM aislado, no sustituyen la revision visual en un navegador real. El estado actual de pruebas/pendientes esta en [TESTING.md](TESTING.md) y [ESTADO-0.2.0.md](ESTADO-0.2.0.md). Para repetirlas con Node 22.20+/24 y npm:

Resultado de esa etapa: 110 pruebas .NET ejecutadas, 104 aprobadas y los mismos seis fallos previos de almacenamiento/preparacion SQLite. Las 30 comprobaciones del DOM aislado pasaron, incluyendo campos historicos, cancelacion sin POST, confirmacion con un solo envio, duplicados al salir del campo, adjuntos y reinicializacion HTMX. No habia un navegador conectado disponible para verificar visualmente el ensayo.

```powershell
npm install --prefix tmp/form-dom-tests --no-save --ignore-scripts happy-dom@21.0.3
node scripts/validation/Test-FormFeedback.cjs
```

Happy DOM y sus dependencias son solo herramientas de prueba bajo tmp; no se publican con la aplicacion.

Referencias: [validacion jQuery](https://jqueryvalidation.org/validate/), [Toast de Bootstrap](https://getbootstrap.com/docs/5.3/components/toasts/), [Modal de Bootstrap](https://getbootstrap.com/docs/5.3/components/modal/).
