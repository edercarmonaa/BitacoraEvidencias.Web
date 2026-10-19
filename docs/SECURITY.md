# Seguridad de 0.2.0

La version SQL requiere conexion y llave externas. Produccion SQLite permanece sin sustituir; no heredar como comprobados los controles de la version anterior sin ensayarlos en SQL.

## Aplicacion

Cookies HttpOnly/seguras, autenticacion y roles Admin/Capturista, expiracion y refresco de seguridad de sesion. Contrasenas web almacenadas con hash/sal; migracion conserva esos valores y no recrea usuarios. Validacion de servidor, antiforgery y autorizacion se mantienen aunque JavaScript no este disponible.

La llave BITACORA_DATA_PROTECTION_KEY es obligatoria en 0.2.0 y debe ser la misma al migrar/restaurar. Cifra datos/archivos y permite busquedas protegidas. No imprimirla, guardarla en Git ni incluirla en el ZIP de datos. Los historicos exportan valores fisicos sin descifrarlos, pero siguen conteniendo informacion sensible.

## SQL y TLS

Conexion por usuario/contrasena SQL; verificar usuario de operacion separado de la cuenta administradora. La migracion/backup/operaciones necesitan permisos distintos, no concedidos automaticamente por los scripts. No configurar sa como cuenta normal de la aplicacion por haberlo usado en el ensayo.

Los wrappers SQL exigen cifrado y comprueban confianza por defecto. TrustLocalSqlCertificate es una excepcion explicita de identidad del certificado local, no un modo sin cifrado ni una solucion para el certificado HTTPS. La configuracion final de ambos certificados sigue pendiente de aceptacion.

Las cadenas de conexion y contrasenas no deben aparecer en argumentos, transcripciones, logs ni reportes. El monitor y archivador registran agregados/estados, no mensajes originales del proveedor. DPAPI protege credenciales de tareas para una cuenta Windows/equipo; no protege frente a procesos que ya actuan como esa identidad.

## Archivos y continuidad

Restringir ACL de evidencias, historicos, BAK de intercambio, respaldos y archivos credential.xml. El servicio SQL escribe el BAK; la cuenta Windows de backup lo lee y empaqueta. No otorgar permisos amplios sobre toda la instalacion para corregir una carpeta.

El backup completo incluye SQL, evidencias e historicos y compara SHA256. La llave, credenciales de aplicacion, certificado/contrasena y configuracion necesitan custodia separada. VERIFYONLY y hashes no sustituyen recuperar y validar la aplicacion.

El control de mantenimiento usa una canalizacion local de la misma identidad Windows, sin HTTP. Mantiene temporalmente secretos de reinicio en memoria, no en un archivo de estado. Requiere host independiente y elevacion compatible con la tarea; la interrupcion del equipo/tarea exige arranque habitual.

## Repositorio

No versionar datos, bases SQL/SQLite, BAK, fotografias, historicos, manifiestos operativos, credenciales DPAPI, certificados, llaves ni logs. Los informes internos y evidencias de pruebas no deben incorporarse por actualizar documentacion. Revisar tambien historial/etiquetas antes de publicar, sin reescribirlos automaticamente.

Guias: [privacidad](PRIVACY-DATA.md), [cifrado](CIFRADO-DATOS-EVIDENCIAS.md), [backup](BACKUP-OPERACION.md), [operaciones SQL](AUDITORIA-MONITOREO-0.2.0.md) y [pendientes](ESTADO-0.2.0.md).
