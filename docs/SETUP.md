# Preparar el ensayo SQL Server 0.2.0

Actualizado: 19/10/2026. Para SQLite consultar [SQLITE-0.1.md](SQLITE-0.1.md). La version SQL no crea automaticamente base, usuarios ni datos al arrancar.

## Requisitos

Windows y PowerShell para los scripts y procesamiento de imagenes, SDK fijado por `global.json` (9.0.319, latestPatch), runtime ASP.NET Core 9 para binarios publicados y SQL Server Express 2025 local, instancia `.\Auditoria`. Los scripts de migracion fijan esa instancia y las bases `BitacoraEvidencias_Pruebas`/`BitacoraEvidencias`.

La base de ensayo actual ya contiene datos. Para repetir una importacion se necesita otro ensayo controlado con destino vacio; no borrar ni reinicializar la base existente. No se incluye una base de muestra ni un administrador predeterminado para 0.2.0.

## Compilacion

```powershell
dotnet restore
dotnet build tools/BitacoraMigration/BitacoraMigration.csproj -c Release
```

Si no hay acceso al servicio de auditoria NuGet, la excepcion temporal `-p:NuGetAudit=false` no constituye una comprobacion de vulnerabilidades. Registrar la limitacion y completar esa revision antes de liberar.

## Configuracion externa

Los appsettings versionados tienen conexion vacia y Storage relativo. `WebApplication.CreateBuilder` lee configuracion estandar y variables de entorno; no hay cargador `.env` ni registro de `appsettings.Local.json`. Usar [.env.example](../.env.example) como referencia, sin asumir carga automatica.

La cadena local tiene esta estructura, con valores de ejemplo que no son credenciales operativas:

```text
Server=.\Auditoria;Database=BitacoraEvidencias_Pruebas;User ID=YOUR_SQL_USER;Password=YOUR_LOCAL_PASSWORD;Encrypt=True;TrustServerCertificate=False
```

Para el ensayo de datos existentes se requiere la misma `BITACORA_DATA_PROTECTION_KEY`, el certificado HTTPS y su contrasena, suministrados por el mecanismo externo autorizado. No imprimirlos ni generar una llave nueva. El lanzador SQL solicita las credenciales y ajusta la conexion solo en su proceso.

Mantener desactivados PrepareApplicationData, ResetApplicationData, CheckLegacySchemaOnly, StampLegacyBaselineOnly y ProtectExistingData. Program.cs rechaza esas opciones en 0.2.0. Una conexion valida sin esquema completo/usuarios migrados no basta para arrancar.

## Datos y arranque

Seguir [ENSAYO-SQLSERVER.md](ENSAYO-SQLSERVER.md): extraer una copia nueva, preflight, import sobre destino vacio y verify antes de probar escrituras. Los datos y archivos del ensayo viven en `C:\BitacoraEvidencias_Pruebas`; produccion sigue en `C:\BitacoraEvidencias`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Publish-BitacoraSqlServer.ps1 -AppFolder App-ensayo-nuevo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/deploy/Start-BitacoraSqlServer.ps1 -AppFolder App-ensayo-nuevo
```

La carpeta de publicacion debe ser nueva. El lanzador usa Production, HTTPS 5068 y `Input\Evidencias`; no modifica variables persistentes. Si el certificado HTTPS cubre otro nombre, usar ServerAddress. La excepcion de confianza SQL es independiente de HTTPS.

`dotnet run` solo sirve con conexion, llave, storage y esquema ya preparados. El perfil del repositorio usa HTTPS 127.0.0.1:5068/Development; no crea una base de desarrollo. No habilitar Development en la instalacion productiva.

Los scripts no modifican la politica persistente de PowerShell; el ejemplo Bypass se limita al proceso. Para pruebas nuevas usar datos sinteticos. Estado y pendientes: [ESTADO-0.2.0.md](ESTADO-0.2.0.md).
