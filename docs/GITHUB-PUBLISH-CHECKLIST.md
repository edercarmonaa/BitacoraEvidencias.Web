# Revision antes de publicar en GitHub

Publicar codigo no equivale a migrar produccion ni a aprobar v0.2.0. No mover v0.1.0/archive/sqlite-0.1 para corregir documentos. Esta revision no hace push ni reescribe historial.

## Excluir

Bases SQL/SQLite y BAK, fotografias/miniaturas, historicos JSONL/log/manifiestos, importaciones/exports, reportes de monitor/smoke, credenciales DPAPI, certificados/llaves, .env y configuracion con secretos. No incorporar informes internos ignorados como parte de una actualizacion documental.

Mantener placeholders en ejemplos. Revisar tambien documentos antiguos con nombres, hostnames, rutas personales o identificadores parciales; una advertencia de documento historico no anonimiza su contenido.

## Revisar lo que realmente se versiona

```powershell
git status --short
git diff --cached --stat
git diff --cached --check
git ls-files '*.db' '*.db-wal' '*.db-shm' '*.bak' '*.mdf' '*.ldf' '*.pfx' '*.key' '*.credential.xml' '*.zip' '*.jsonl' '*.log'
```

Los nombres y placeholders de configuracion no son secretos por si mismos. Si se usa un detector, revisar coincidencias de forma privada y no pegar valores encontrados en chat/issues. Evitar busquedas sobre archivos de entorno o respaldos que impriman sus contenidos en consola.

Verificar enlaces locales, referencias de scripts y el [indice](INDEX.md). Documentos vigentes deben indicar SQL Server, puerto de ensayo 5068 y estado real de tareas; procedimientos SQLite deben estar marcados como historicos. Mantener separados resultados de cada etapa y no declarar todas las pruebas aprobadas mientras existan fallos conocidos.

Si un secreto ya se versiono, .gitignore no lo retira del historial: coordinar rotacion y saneamiento especifico antes de publicar. No resolverlo con un force-push o borrado de etiquetas por iniciativa de esta revision.

Ver [SECURITY.md](SECURITY.md), [PRIVACY-DATA.md](PRIVACY-DATA.md), [TESTING.md](TESTING.md) y [ESTADO-0.2.0.md](ESTADO-0.2.0.md).
