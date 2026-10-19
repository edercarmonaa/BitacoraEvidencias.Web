# Recuperacion y traslado SQL Server 0.2.0

Guia preparada el 19/10/2026. En el ensayo previo se probo restauracion SQL y DBCC CHECKDB sin incidencias. En el [corte productivo](CORTE-PRODUCTIVO-0.2.0.md), CHECKDB del nuevo BAK restaurado aprobo con MAXDOP=1 y los cinco conteos coincidieron tras resolver la presion de memoria que impidio los primeros intentos. Recuperacion integral de la aplicacion y traslado a otro equipo siguen pendientes. No ejecutar sobre una base existente para probarla.

## Material necesario

- ZIP completo verificado: Data/*.bak, Evidencias, AuditArchive y backup-manifest.json. Un DatabaseOnly no incluye los archivos; un ZIP SQLite no se restaura como SQL.
- Misma BITACORA_DATA_PROTECTION_KEY, certificado HTTPS/contrasena, configuracion y binarios compatibles, guardados fuera del ZIP de datos.
- SQL Server compatible con el BAK, espacio suficiente, permisos del servicio SQL para leerlo y cuenta administradora para restaurar.
- Informacion de logins/permisos SQL y acceso a las credenciales por el mecanismo externo autorizado. Los usuarios web ya estan en la base y no se recrean.

## Ensayo de restauracion

1. Elegir destino aislado y una base nueva, comprobar que no existan sus archivos ni su nombre. Extraer el ZIP en carpeta nueva; comparar todos los tamanos/SHA256 del manifiesto. No usar el extractor de ensayo SQLite, que espera otro formato.
2. SQL Server debe poder leer el BAK. Ejecutar RESTORE VERIFYONLY y RESTORE FILELISTONLY para conocer archivos logicos. Restaurar hacia archivos nuevos con WITH MOVE para cada archivo, nunca WITH REPLACE sobre una base existente. Ver [restauracion a otra ubicacion](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-database-to-a-new-location-sql-server).
3. Ejecutar DBCC CHECKDB, comprobar historial EF, tablas, conteos/relaciones y estado online. No ejecutar import SQLite sobre la base restaurada.
4. Recuperar Evidencias y AuditArchive a rutas separadas, con permisos del usuario de la aplicacion. Mantener estructura/nombres/cifrado; no renombrar carpetas ni eliminar historicos.
5. Arrancar una publicacion independiente con esa conexion, storage recuperado y misma llave, sin habilitar prepare/reset/stamp/protect. Los scripts del repositorio fijan nombres/instancia para ensayo y produccion; si se usa otro nombre, preparar configuracion especifica y revisar tambien controles de backup/operaciones.
6. Validar usuarios existentes, roles, datos cifrados, originales/miniaturas, busquedas y escritura controlada. Registrar duracion y resultado sin datos personales ni secretos. Solo entonces considerar recuperable el conjunto.

## Traslado a otra PC

Preparar Windows, runtime, SQL y almacenamiento antes de transferir. Los logins de servidor no quedan resueltos solo por restaurar los usuarios de la base: revisar su correspondencia/SID y permisos, sin restablecer cuentas web. Ver [transferencia de logins](https://learn.microsoft.com/en-us/troubleshoot/sql/security/transfer-logins-passwords-between-instances).

Los archivos credential.xml de DPAPI no funcionan en otro equipo/usuario. Prepararlos de nuevo con la identidad Windows de las nuevas tareas, suministrando credenciales localmente; no copiar esos archivos como unico mecanismo de recuperacion. Esta restriccion esta documentada en [Export-Clixml](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/export-clixml?view=powershell-5.1).

Revisar nombre/direccion cubierta por HTTPS y confianza TLS SQL, rutas, cuenta Windows, ACL, firewall y zona de Mexico. Cambiar de equipo/certificado no autoriza cambiar la llave de datos. Preparar tareas deshabilitadas, ensayar y activar solo al aceptar el cambio; no dejar dos instalaciones escribiendo el mismo conjunto de evidencias.

Para regresar a SQLite seguir [MIGRACION-0.2.0.md](MIGRACION-0.2.0.md): una base SQL con escrituras nuevas no se convierte ni concilia por restaurar el archivo SQLite antiguo.
