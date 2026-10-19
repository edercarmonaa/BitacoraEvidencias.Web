# Cifrado de datos y evidencias

Actualizado: 19/10/2026. El esquema de proteccion de SQLite 0.1.0 se conserva en SQL Server 0.2.0. La migracion transfiere valores fisicos sin recifrarlos; Program.cs SQL exige que exista la llave externa actual.

## Llave y alcance

BITACORA_DATA_PROTECTION_KEY debe ser la misma en operacion, ensayo, transferencia y recuperacion. No imprimirla, reemplazarla ni incluirla en Git o el ZIP de datos. Custodiarla mediante el mecanismo externo acordado; disponer de los archivos sin la llave no permite recuperar su contenido protegido.

Comprobar solo presencia, sin mostrar valor:

```powershell
-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('BITACORA_DATA_PROTECTION_KEY','Process'))
```

El lanzador SQL requiere la llave en el proceso. No asumir que una variable persistente ya este disponible en una consola iniciada previamente. La documentacion no configura ni cambia secretos del equipo.

## Datos

Se protegen en CasosCorreccion: Curp, NombreCompleto, Cct, Folio, EvidenciaUrl, Observaciones y Validador. CurpHash, CctHash y FolioHash sirven para busqueda exacta HMAC; el cifrado no permite busqueda parcial equivalente a LIKE sobre el texto protegido.

AuditLogs conserva hashes/sufijos de CURP/CCT/folio cuando aplica la proteccion. Los registros historicos se transfieren tal como estan; no se regeneran ni completan automaticamente. El archivo SQL exporta todas las columnas fisicas, nulos e indices de apoyo sin descifrar y sigue siendo informacion sensible.

Originales/miniaturas se cifran con AES-GCM al guardar, con prefijo BITACORA-ENC-V1. La aplicacion descifra en memoria por endpoint autenticado. Las carpetas nuevas de oficio usan identificador HMAC; no renombrarlas manualmente para recuperar numeros visibles.

## Migracion y recuperacion

Preflight comprueba lectura de valores/archivos con la llave existente sobre copias. Import conserva cifrado y referencias; verify compara valores fisicos. Despues comprobar visualmente datos, originales y miniaturas desde la aplicacion.

Protect-ExistingData.ps1 es un procedimiento historico SQLite. Su flag de arranque esta bloqueado en 0.2.0: no ejecutarlo como paso de la migracion SQL ni intentar recifrar la instalacion con una llave nueva. El procedimiento original se conserva en v0.1.0; [SQLITE-0.1.md](SQLITE-0.1.md) indica como consultarlo sin cambiar el repositorio operativo.

El backup SQL incluye BAK, fotos e historicos en su estado fisico, con SHA256; excluye llave, certificado y contrasenas. Para recuperar se necesita el conjunto de datos y la custodia externa de esos elementos. Una prueba SQL correcta no demuestra por si sola que el navegador pueda leer todo el contenido cifrado.

Guias: [migracion](MIGRACION-0.2.0.md), [backup](BACKUP-OPERACION.md), [recuperacion](RECUPERACION-SQLSERVER.md) y [seguridad](SECURITY.md).
