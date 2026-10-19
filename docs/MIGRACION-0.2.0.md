# Acuerdos y cambio de SQLite 0.1.0 a SQL Server 0.2.0

Actualizado: 19/10/2026. [SQL productivo activo](CORTE-PRODUCTIVO-0.2.0.md): respaldo final SQLite creado, SQL importado/verificado, servicio https://192.168.1.95:5067 confirmado por el responsable y tareas SQL habilitadas tras un ciclo real de backup/reinicio aprobado. Queda pendiente CHECKDB de la copia restaurada por falta de RAM. No repetir import ni usar el corte de ensayo como origen. La secuencia siguiente queda como referencia del procedimiento.

## Acuerdos

- SQLite 0.1.0 queda congelada sin funcionalidades nuevas, en v0.1.0 y archive/sqlite-0.1. Se integraron los seis commits documentales pendientes antes de fijarla; esa integracion ya no es un pendiente.
- Nuevas funcionalidades solo en 0.2.0 SQL Server Express 2025, rama feature/sqlserver-0.2. Ambas versiones coexisten en el historial, sin sincronizacion automatica de bases.
- Mismo equipo/cuenta Windows; SQL Express ya instalado, instancia .\Auditoria, autenticacion SQL usuario/contrasena.
- Bases existentes: BitacoraEvidencias ya contiene el corte productivo verificado; BitacoraEvidencias_Pruebas conserva datos del ensayo. No eliminar/recrear bases ni usuarios, ni repetir import.
- Produccion C:\BitacoraEvidencias / 5067; ensayo C:\BitacoraEvidencias_Pruebas / 5068 con copias de evidencias.
- Secretos/configuracion en variables externas. Credenciales solicitadas localmente; nunca en chat, argumentos visibles ni Git.
- Se conserva toda la informacion: identificadores/relaciones, hashes/sales/contrasenas existentes, roles, cuentas, cifrado y llave, fotografias, miniaturas e historicos de auditoria.
- Nuevas validaciones no completan ni rechazan automaticamente los historicos incompletos al transferirlos. Reglas en [VALIDACIONES-0.2.0.md](VALIDACIONES-0.2.0.md).
- Ventana sin usuarios autorizada. Horarios de Mexico: monitor 09:15, archivo 09:20, backup 14:00. Auditoria: 30 dias en SQL y anteriores en archivos conservados.

## Ensayo ya realizado

El respaldo inicial SQLite de octubre se extrajo y comparo (3,658 archivos totales); preflight, import y verify coincidieron en las cinco tablas antes de escrituras. Los conteos iniciales y pruebas posteriores estan fechados en el estado de aceptacion. La copia de ensayo no es un corte final actualizado de produccion.

Las correcciones de DbContext, Auditoria y Busquedas estan incorporadas a las publicaciones posteriores. La mas reciente comprobada es App-backup-final, que tambien contiene validaciones y control de reinicio. Los totales de pruebas de etapas antiguas no sustituyen la evidencia actual.

Procedimiento reproducible: [ENSAYO-SQLSERVER.md](ENSAYO-SQLSERVER.md). No repetir import en la base ocupada ni verify contra el origen antiguo despues de escribir datos de prueba.

## Conservacion y preparacion final

1. Resolver/aceptar pendientes del [estado](ESTADO-0.2.0.md), comprobar usuario SQL de operacion y confianza de certificados.
2. Preparar publicacion/lanzador productivo SQL, credenciales DPAPI y permisos. Los scripts SqlServer actuales siguen siendo de ensayo; los nuevos SqlRelease/SqlProduction preparan un despliegue independiente, sin reemplazar el acceso directo ni activar produccion. Ver [SCRIPTS-0.2.0.md](SCRIPTS-0.2.0.md).
3. Acordar mantenimiento y responsable de aceptar el resultado; no deducir que produccion esta detenida de una inspeccion anterior.
4. Detener todos los escritores, incluidas tareas SQLite de archivado, y generar corte final consistente.
5. Respaldar SQLite/evidencias con el procedimiento 0.1.0 conservado y guardar ademas AuditArchive (historicos ya retirados de SQLite), binarios, configuracion, certificados y secretos por mecanismos protegidos. La llave se custodia por separado, no dentro del ZIP. Verificar recuperacion.
6. Extraer una copia nueva de ese corte fuera de C:\BitacoraEvidencias y pasar explicitamente SourceDatabase/SourceEvidence. Las herramientas rechazan usar el origen productivo directamente.

## Secuencia del cambio productivo (referencia)

1. Verificar nuevamente que la base existente BitacoraEvidencias esta vacia y los permisos efectivos son correctos. Si contiene datos, detenerse; no limpiarla automaticamente.
2. Ejecutar preflight de la copia final; despues import con Production sobre esa misma copia y verificar antes de pruebas de escritura. No reutilizar Input antiguo como si fuera el corte final.
3. Desplegar la publicacion SQL independiente con conexion productiva, puerto 5067, evidencias existentes, misma cuenta y misma llave/certificado. Conservar binarios/configuracion/arranque SQLite.
4. Comprobar login/cuentas/roles, lectura protegida, originales/miniaturas, indices, consultas y secuencias; registrar cualquier escritura controlada antes de permitir usuarios.
5. Deshabilitar tareas SQLite y registrar/habilitar SQL solo durante este cambio, tras ensayar sus credenciales y rutas. El backup SQL completo incluye historicos anteriores.
6. Aceptar el resultado y reabrir el servicio. Mantener codigo, base y respaldos SQLite despues de aprobar.

No se incluyen comandos para borrar bases, restablecer usuarios o regenerar secretos. El importador conserva datos fisicos y confirma esquema/datos en transaccion; no sustituye validacion funcional ni custodia externa.

## Recuperacion

Si falla antes de admitir usuarios, detener SQL, recuperar binarios/configuracion SQLite y restaurar el conjunto final consistente si las pruebas alteraron archivos compartidos. Despues de escrituras nuevas en SQL, volver exige transferir/conciliar esos cambios; arrancar la base SQLite anterior los perderia.

Recuperacion SQL y traslado: [RECUPERACION-SQLSERVER.md](RECUPERACION-SQLSERVER.md). No crear v0.2.0 ni publicar al remoto como si el cambio estuviera aprobado antes de completar la aceptacion.
