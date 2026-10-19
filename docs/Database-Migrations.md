# Migraciones de base de datos

## Proveedores separados

0.2.0 registra `SqlServerAppDbContext`, derivado de AppDbContext, con UseSqlServer. `Data/SqlServerMigrations` contiene `InitialSqlServer` y `PagingIndexes`; `Data/Migrations` conserva el historial SQLite. No mezclar historiales ni aplicar el stamp SQLite a SQL Server.

El arranque exige conexion y llave externas, conectividad, migraciones aplicadas y usuarios existentes. Bloquea los flags SQLite de preparacion/reset/stamp/proteccion. No migra ni crea usuarios automaticamente al iniciar.

## Transferencia 0.1.0 a 0.2.0

```powershell
dotnet build tools/BitacoraMigration/BitacoraMigration.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/migration/Invoke-BitacoraSqlMigration.ps1 -Mode preflight
```

Preflight lee una copia SQLite/evidencias fuera de produccion y valida integridad, relaciones, esquema, longitudes, archivos y descifrado. Para un destino SQL de pruebas vacio, el procedimiento [ENSAYO-SQLSERVER.md](ENSAYO-SQLSERVER.md) detalla import y verify. La base de pruebas actual ya tiene datos: no repetir import.

| Modo | Comportamiento |
| --- | --- |
| preflight | Solo copia de origen; no requiere conexion SQL. |
| import | Crea esquema y transfiere todos los campos/identificadores en transaccion; rechaza destino no vacio. |
| verify | Compara tablas/filas/campos y evidencias contra el respaldo original. Ejecutar antes de escrituras de prueba. |
| upgrade | Solo aplica PagingIndexes si corresponde a una base SQL ya importada; no reimporta datos. |
| profile | Diagnostico de Busquedas en pruebas; rechaza Production. |

Instancia fija `.\Auditoria`; destino de pruebas `BitacoraEvidencias_Pruebas`. Production selecciona `BitacoraEvidencias`; import/upgrade exigen ademas confirmacion local de mantenimiento. El responsable debe revisar estado vacio y permisos, sin eliminar/recrear la base ni el usuario existente. Nunca apuntar SourceDatabase/SourceEvidence a las rutas productivas: aun en modo Production se usan copias.

## Cambios futuros de esquema

Con dotnet-ef disponible en una version compatible con EF Core 9:

```powershell
dotnet ef migrations add NombreDeLaMigracion --context SqlServerAppDbContext --output-dir Data/SqlServerMigrations
```

La factory de diseno sirve para generar migraciones sin conectarse; su conexion ficticia no inicializa una base operativa. Revisar SQL generado, modelo, tipos, indices y pruebas antes de aplicar cambios. El importador/upgrade admiten explicitamente las dos migraciones actuales: una migracion nueva requiere adaptar y validar ese proceso. No ejecutar un database update generico sobre produccion como sustituto del plan.

Los scripts `Initialize-BitacoraDatabase.ps1`, `Test-BitacoraLegacySchema.ps1` y `Stamp-BitacoraLegacyBaseline.ps1` son historicos SQLite y deben usarse con el codigo 0.1.0 conservado, no con Program.cs SQL. Consultar [SQLITE-0.1.md](SQLITE-0.1.md).
