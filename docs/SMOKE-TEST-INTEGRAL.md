# Aceptacion funcional SQL Server 0.2.0

Solo sobre copias de ensayo, con respaldo y permiso para crear/cambiar/borrar datos. Los smokes no son pruebas de solo lectura y dejan de ser comparables con verify del respaldo inicial despues de escribir.

## Preparacion

1. Arrancar una publicacion SQL reciente en 5068, base BitacoraEvidencias_Pruebas y evidencias de copia; no usar los lanzadores SQLite ni puerto productivo.
2. Comprobar llave/certificado externos sin imprimir sus valores, conectividad, migraciones y usuarios migrados.
3. Disponer de cuentas Admin/Capturista existentes de ensayo y datos/imagenes sinteticos. No hay contrasena bootstrap predeterminada ni creacion automatica de usuarios SQL.
4. Revisar los scripts smoke antes de usarlos con las nuevas reglas: sus payloads historicos no constituyen aceptacion automatica de formularios 0.2.0.

## Comprobaciones manuales

- Acceso anonimo restringido, login/logout, bloqueo y permisos por rol; cambios administrativos afectan sesiones segun el flujo de seguridad.
- Oficio obligatorio/unico sin distinguir mayusculas; fecha no futura; asunto y restricciones de formulario.
- Oficio vacio permitido; maximo un caso de Primaria y uno de Secundaria; eliminar el ultimo caso permitido.
- CURP/CCT/nombre/periodos/fecha de verificacion obligatorios al crear; folio/validador opcionales. CURP/CCT por formato, sin padron. CCT antiguas segun reglas confirmadas.
- Verificacion no futura ni anterior al oficio; fin de periodo no anterior al inicio. Preservar historicos incompletos sin cambios y rechazar cambios nuevos invalidos.
- Errores al salir del campo, junto al control y resumen toast al enviar; aceptar/cancelar confirmaciones, un solo POST, valores conservados y navegacion HTMX.
- Originales/miniaturas legibles mediante endpoint autenticado; archivo fisico conserva cifrado. Validacion de adjuntos y enlaces HTTP/HTTPS.
- Busquedas, filtros, orden, primera/ultima pagina, pagina vacia, galeria y cambio de modulo sin demoras anteriores.
- Importacion con vista previa y reglas nuevas, reportes, auditoria y administracion de usuarios; eventos esperados sin datos sensibles en consola.

Detalles de reglas: [VALIDACIONES-0.2.0.md](VALIDACIONES-0.2.0.md). No aplicar verificaciones PRAGMA/WAL SQLite como criterio del servidor SQL.

## Smoke automatizado opcional

Los scripts admiten BITACORA_SMOKE_ADMIN_PASSWORD. Introducirla localmente, mantenerla solo en el proceso y limpiarla al terminar. Evitar AdminPass en argumentos. Indicar siempre URL y StorageRoot de ensayo:

```powershell
# Requiere la variable de proceso ya suministrada por el mecanismo local seguro.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/smoke/Run-Smoke.ps1 -BaseUrl https://127.0.0.1:5068 -StorageRoot C:\BitacoraEvidencias_Pruebas\Input\Evidencias
```

Los smokes existentes aceptan el certificado HTTPS de laboratorio sin validar confianza; no prueban su seguridad ni sustituyen el navegador con certificado confiable. No prometer que todos sus payloads estan actualizados hasta revisar/ejecutar el ensayo correspondiente.

## Evidencia de aceptacion

Registrar fecha, publicacion/commit, proveedor/base/puerto, rol, flujo, resultado y limitacion sin contrasenas ni datos personales. Adjuntar resultados operativos solo fuera de Git. Revisar errores/alertas de monitor y el ciclo backup/restauracion por separado.

La confirmacion anterior de funcionamiento de Busquedas no equivale a aceptar formularios nuevos. Pendientes en [ESTADO-0.2.0.md](ESTADO-0.2.0.md); resultados automatizados en [TESTING.md](TESTING.md).
