# Privacidad y conservacion de datos

Oficios, casos, fotos, usuarios, bitacora e importaciones pueden contener datos personales. En 0.2.0 SQL guarda metadatos/registros; evidencias e historicos siguen siendo archivos sensibles fuera de Git. Migrar o cifrar no anonimiza la informacion.

## Conservacion acordada

- Migrar todas las filas/campos/relaciones, hashes y sales de cuentas, roles, datos protegidos y referencias de originales/miniaturas.
- No recrear usuarios, restablecer contrasenas ni regenerar la llave durante la transferencia.
- Conservar historicos incompletos conforme a las reglas de edicion; no completar campos ficticios para que pasen validaciones nuevas.
- Auditoria: 30 dias en SQL; anteriores en historicos verificados, sin eliminacion automatica de esos archivos.
- Backup: retencion predeterminada 30 dias solo para ZIP SQL finalizados de la base correspondiente, despues de un backup nuevo verificado. No es retencion de expedientes ni de historicos.
- Mantener el conjunto SQLite anterior tras el cambio, con acceso restringido; el historial Git conserva codigo, no datos.

## Fuera del repositorio

No publicar nombres/identificadores reales, fotografias, BAK/bases, auditoria JSONL/log, importaciones, credenciales DPAPI, reportes de monitor/smoke, certificados o llaves. Los manifiestos operativos pueden revelar rutas/identificadores: tratarlos con la misma reserva que el respaldo.

Los ejemplos de documentacion usan variables/placeholders y resultados agregados. No copiar la salida completa de una excepcion SQL o los valores de entorno a chat/issues. Los informes internos existentes conservan su contexto y requieren revision especifica antes de cualquier publicacion.

## Recuperacion y acceso

Restringir permisos de almacenamiento y custodiar la misma llave por separado del ZIP. Los archivos de credenciales DPAPI no sustituyen una custodia recuperable de secretos y no son trasladables a otra cuenta/equipo. Registrar accesos/operaciones sin imprimir secretos.

Ver [SECURITY.md](SECURITY.md), [AUDITORIA-MONITOREO-0.2.0.md](AUDITORIA-MONITOREO-0.2.0.md), [RECUPERACION-SQLSERVER.md](RECUPERACION-SQLSERVER.md) y [GITHUB-PUBLISH-CHECKLIST.md](GITHUB-PUBLISH-CHECKLIST.md).
