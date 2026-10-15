# Acuerdos para la version 0.2.0

Estado: requisitos acordados; implementacion y ensayo pendientes. Este documento no cambia la configuracion de produccion.

## Versiones y conservacion del codigo

- La instalacion productiva corresponde al codigo actual del repositorio, segun confirmacion del responsable.
- La version 0.1.0 usa SQLite y se conservara congelada, sin nuevas funcionalidades.
- La version 0.2.0 usara SQL Server Express 2025; las nuevas funcionalidades se desarrollaran sobre esta version.
- Antes de modificar la persistencia, registrar la base SQLite completa en un commit y una etiqueta anotada `v0.1.0`. Conservar una rama `archive/sqlite-0.1` en ese punto y desarrollar en `feature/sqlserver-0.2`.
- Ambas versiones coexistiran en el historial del mismo repositorio. No se requiere mantener dos aplicaciones activas ni sincronizar ambos gestores.
- La rama local tenia seis commits pendientes de incorporar desde `origin/main` al revisar el versionado. Revisar e integrar esos cambios antes de fijar la referencia definitiva.
- La ultima ejecucion de pruebas reporto 56 aprobadas y 6 fallidas en almacenamiento y preparacion de base. Investigar y registrar estos fallos antes de aceptar la migracion.

## Entorno confirmado

- Migracion en el mismo servidor; SQL Server Express 2025 ya instalado.
- Autenticacion de SQL Server mediante usuario y contrasena.
- Instancia SQL Server: `Auditoria`; conexion local prevista: `.\Auditoria`.
- Base de produccion: `BitacoraEvidencias`.
- Base aislada para el ensayo: `BitacoraEvidencias_Pruebas`, ya creada y vacia segun confirmacion del responsable.
- Carpeta independiente del ensayo autorizada: `C:\BitacoraEvidencias_Pruebas`, con binarios, configuracion, logs y copias de evidencias separados de produccion.
- Puerto previsto para el ensayo: `5068`; comprobar disponibilidad antes de arrancar. Configurar las variables de prueba solo en el proceso de ensayo, sin sustituir variables persistentes de produccion.
- Misma cuenta de Windows para ejecutar la aplicacion.
- Configuracion y secretos actuales en variables de entorno.
- Base SQLite de aproximadamente 3.14 MB.
- Fotografias y evidencias conservaran sus carpetas actuales en produccion.
- Existe respaldo reciente de base, evidencias y variables de entorno con secretos, segun confirmacion del responsable.
- Se permite ventana de mantenimiento sin usuarios operando.
- Se autoriza ensayo previo con copias del respaldo y base SQL Server de pruebas. Las evidencias usadas por el ensayo seran copias.
- El servidor productivo, SQL Server y el repositorio de desarrollo estan en el mismo equipo.
- El responsable confirma que ya existen la base de produccion `BitacoraEvidencias`, vacia y sin tablas, y su usuario SQL. Reutilizar esa base sin eliminarla ni recrear el usuario; comprobar que este vacia antes de crear el esquema y migrar los datos.
- Ambas bases ya existen y estan vacias, segun confirmacion del responsable. No eliminarlas ni recrearlas: verificar su estado antes de preparar el esquema. El responsable confirma que el usuario SQL de la aplicacion es propietario de ambas bases; verificar los permisos efectivos al conectar. Se dispone ademas de una cuenta administradora distinta. Revisar los permisos necesarios para la operacion diaria antes del cambio productivo, sin alterar los actuales durante el levantamiento de requisitos.
- La cuenta administradora usa autenticacion SQL (usuario y contrasena) y es distinta del usuario SQL de la aplicacion. Usar la administradora exclusivamente para la preparacion y las operaciones que requieran privilegios; configurar la aplicacion con permisos de operacion limitados.
- Solicitar credenciales localmente al ejecutar los procedimientos, sin incluirlas en argumentos visibles, reportes, archivos versionados ni mensajes del chat.
- El responsable confirmo haber iniciado produccion despues de la revision de solo lectura. No tratar el servidor como detenido ni ejecutar mantenimiento sin coordinar su ventana.

## Revision local de solo lectura (2026-10-15)

- Instalacion: `C:\BitacoraEvidencias`; binarios publicados en `deploy\current`.
- Se encontro `Data\bitacora-evidencias.db` (3,293,184 bytes). El lanzador del repositorio configura esta base y la carpeta `Evidencias` de la instalacion.
- El acceso directo `Iniciar Bitacora Manual.cmd` referencia el lanzador del repositorio, no una copia independiente. Conservar ese arranque SQLite antes de modificar los scripts para 0.2.0.
- El archivo de configuracion publicado tiene rutas relativas por defecto; el lanzador las sustituye con variables de entorno del proceso. No deducir las rutas activas solo de ese archivo.
- Se encontraron respaldos en `BackupsBitacora`; el mas reciente observado fue `BitacoraBackup_20261014_091416.zip`. Su contenido y restaurabilidad no se han verificado.
- Se confirmo presencia de la llave de proteccion en ambitos Process y Machine; certificado y su contrasena en Process. No se mostraron ni guardaron sus valores. Comprobar su disponibilidad en el contexto de cada arranque.
- Servicio `MSSQL$AUDITORIA` en ejecucion; conexion y permisos de bases aun no probados.
- No se encontro un proceso llamado `BitacoraEvidencias.Web` durante la revision; esto no descarta ejecucion mediante `dotnet` u otro mecanismo. Confirmar el estado operativo antes del ensayo.

## Conservacion obligatoria

- Migrar todos los registros y campos: oficios, casos, evidencias, usuarios y auditoria; conservar identificadores y relaciones.
- Conservar hashes y sales de contrasenas, roles, estados de cuenta y demas atributos de los usuarios. No recrear usuarios ni restablecer contrasenas como parte de la migracion.
- Conservar valores cifrados, indices de busqueda protegidos y la llave actual `BITACORA_DATA_PROTECTION_KEY`; validar que la nueva aplicacion pueda leerlos sin modificar la llave.
- Conservar referencias a originales y miniaturas, archivos y permisos necesarios para su lectura y escritura.
- Conservar configuracion HTTPS, certificados y secretos actuales fuera de Git. Agregar las credenciales SQL por el mecanismo externo de configuracion.
- No guardar valores reales de secretos, datos operativos ni respaldos en el repositorio o los reportes de migracion.

## Implementacion requerida

- Adaptar proveedor EF Core, esquema, migraciones, consultas administrativas y scripts dependientes de SQLite para SQL Server.
- Revisar compatibilidad de tipos, precision de fechas, longitudes reales, restricciones, indices, intercalacion y cascadas. SQLite puede contener valores mas largos que los limites declarados; no truncar ni descartar registros para ajustar el destino.
- Implementar transferencia que conserve valores almacenados, identificadores y relaciones sin activar transformaciones de guardado que alteren auditoria o cifrado.
- Verificar el siguiente identificador generado despues de importar datos.
- Rechazar destinos no vacios y detenerse ante incompatibilidades; documentar como limpiar exclusivamente la base de ensayo y repetir el proceso.
- Actualizar respaldo, restauracion, monitoreo, preparacion y despliegue para SQL Server.

## Ensayo y aceptacion

1. Restaurar copias consistentes de SQLite y evidencias en un entorno aislado de las rutas y puertos productivos, usando la misma llave mediante configuracion externa.
2. Crear una base SQL Server de pruebas y ejecutar la transferencia.
3. Comparar todas las filas y campos almacenados por identificador, conteos, relaciones y secuencias; comprobar integridad y ausencia de registros omitidos. Emitir resultados sin valores personales ni secretos.
4. Verificar archivos y miniaturas contra el respaldo mediante hashes, y lectura de evidencias y datos protegidos desde la aplicacion.
5. Validar inicio de sesion con cuentas existentes, roles, auditoria, consultas, reportes y operaciones de alta, cambio y eliminacion sobre las copias.
6. Ensayar recuperacion y registrar resultado, duracion y pasos reproducibles.

## Cambio en produccion y recuperacion

1. Tras aprobar el ensayo, iniciar mantenimiento y detener todas las instancias que escriban datos o evidencias.
2. Crear un respaldo final consistente de SQLite, evidencias, binarios, configuracion y secretos mediante almacenamiento externo protegido. Verificar que se pueda restaurar.
3. Migrar a una base SQL Server nueva, manteniendo intactos la base SQLite original y el respaldo final.
4. Desplegar 0.2.0 conservando las carpetas de evidencias, cuenta, permisos y secretos; ajustar externamente la conexion SQL.
5. Repetir comparaciones y validaciones antes de permitir escrituras de usuarios.
6. Si la validacion falla antes de reabrir el servicio, detener 0.2.0, restablecer binarios y configuracion 0.1.0 y restaurar el estado consistente del respaldo si la validacion modifico archivos compartidos.
7. Conservar codigo y respaldos SQLite despues de aprobar el cambio. Una vez que 0.2.0 acepte datos nuevos, volver a SQLite requerira un procedimiento de transferencia y conciliacion adicional; arrancar la base antigua perderia esos cambios.

## Datos por definir

- Verificar conectividad a la instancia SQL Server y disponibilidad de los nombres de bases acordados.
- Cuenta SQL y permisos de preparacion, migracion y operacion; credenciales suministradas fuera del chat y de Git.
- Configuracion TLS de SQL Server y confianza del certificado en el servidor.
- Rutas operativas reales y disponibilidad del respaldo para ejecutar el ensayo, mediante acceso local seguro.
- Fecha de mantenimiento y responsable de aceptar la validacion antes de reabrir el servicio.
