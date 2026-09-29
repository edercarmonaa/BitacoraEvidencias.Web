# Prueba de Humo Integral (Seguridad + Operacion)

Aplicacion: Bitacora Evidencias (ASP.NET Core Razor + SQLite)
Objetivo: validar login, bloqueo, timeout, CRUD, evidencias protegidas/cifradas, reportes y bitacora.

## Validacion automatizada minima

1. Ejecutar:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\validation\Run-StartupValidation.ps1`
2. Cobertura:
   - arranque normal falla si falta la DB
   - inicializacion explicita crea DB y storage
   - migracion explicita de `Storage` legado funciona
   - `Start-BitacoraWithHttps.ps1` bloquea arranque sin DB preparada
3. Esperado:
   - todas las validaciones en `PASS`

## 0) Precondiciones

1. Ejecutar la app:
   - `dotnet run`
2. URL base:
   - `https://127.0.0.1:5067`
   - Para laboratorio tambien puedes usar `http://127.0.0.1:<puerto>` si desactivas redireccion HTTPS.
3. Usuario admin operativo:
   - `admin`
   - Debe usar una contrasena conocida por el operador. Ya no existe `Admin123!` por defecto.
4. Base SQLite:
   - `Data/bitacora-evidencias.db`
5. Storage operativo:
   - por defecto `C:\BitacoraEvidencias\Evidencias`
   - ambos smokes aceptan `-StorageRoot` para validar sobre una ruta temporal o no estandar

## 1) Smoke tecnico rapido (CLI)

1. Compilacion:
   - `dotnet build`
   - Esperado: `0 Errores`
2. Variante `HttpClient`:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\smoke\Run-Smoke.ps1 -BaseUrl https://127.0.0.1:5067 -AdminPass "<tu_password_admin>"`
3. Variante `curl.exe`:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\smoke\Run-Smoke-Curl.ps1 -BaseUrl https://127.0.0.1:5067 -AdminPass "<tu_password_admin>"`
4. Password admin por variable de entorno:
   - definir `BITACORA_SMOKE_ADMIN_PASSWORD` y omitir `-AdminPass`
5. Storage no estandar o temporal:
   - agregar `-StorageRoot "<ruta_storage>"`
6. Implementacion compartida:
   - la logica comun vive en `.\scripts\smoke\Smoke.Common.ps1`
7. Verificar que no hay evidencias en `wwwroot`:
   - revisar que solo existan en el `StorageRoot` configurado
8. Si el entorno ya opera con cifrado, validar que exista la llave:
   - `[Environment]::GetEnvironmentVariable("BITACORA_DATA_PROTECTION_KEY", "Machine")`
9. Verificar PRAGMA SQLite aplicado (en logs o conexion runtime):
   - `journal_mode=WAL`
   - `synchronous=NORMAL`
   - `busy_timeout=5000`

## 2) Cobertura del smoke actual

1. Acceso anonimo a rutas protegidas.
2. Login admin, incluyendo cambio forzado de contrasena si el bootstrap lo exige.
3. Bloqueo de cuenta tras 5 intentos fallidos.
4. Revocacion inmediata de rol admin en una sesion ya abierta.
5. CRUD completo de oficio, caso y evidencia.
6. Proteccion de evidencias por endpoint autenticado.
7. Cifrado fisico de evidencias y miniaturas cuando la llave esta configurada.
8. Reportes operativos en orden esperado.
9. Auditoria de `LOGIN`, `LOGOUT`, `LOGIN_FAIL`, `LOCKOUT`, `CREATE`, `UPDATE`, `DELETE`, `UPLOAD_EVIDENCE`, `DELETE_EVIDENCE`, `VIEW_EVIDENCE`.
10. Logout y proteccion posterior.
11. Timeout por inactividad:
   - sigue en `SKIP` automatico; requiere esperar mas de 30 minutos.

## 3) Acceso y autorizacion

### Caso A: anonimo

1. Abrir en incognito:
   - `https://127.0.0.1:5067/Oficios`
2. Esperado:
   - redirige a `/Login`

### Caso B: login correcto

1. Login con admin.
2. Esperado:
   - acceso a Inicio
   - menu Admin visible (Usuarios, Bitacora)

### Caso C: logout

1. Clic en `Cerrar sesion`.
2. Esperado:
   - redirige a Login
   - no permite volver a paginas protegidas sin autenticacion

## 4) Bloqueo de cuenta (5 intentos = 30 min)

Importante: usar un usuario de prueba, no `admin`.

1. Como admin, crear usuario `prueba.lock` (rol Capturista).
2. Cerrar sesion.
3. Intentar login de `prueba.lock` con contrasena incorrecta 5 veces.
4. En el intento 6, aun con la contrasena correcta, validar que sigue bloqueada.
5. Esperado:
   - mensaje de cuenta bloqueada
   - en DB: `BloqueadoHastaUtc` con ~30 min hacia adelante

## 5) Revocacion inmediata de rol

1. Como admin, crear un segundo admin temporal.
2. Iniciar sesion con ese segundo admin y completar su cambio inicial de contrasena.
3. Desde la sesion del primer admin, degradarlo a `Capturista`.
4. Esperado:
   - la sesion ya abierta del usuario degradado pierde acceso a `/Admin/Users` sin relogin
   - la misma sesion sigue pudiendo entrar a rutas generales como `/Oficios`

## 6) Timeout de sesion (30 min inactividad)

Opcion productiva (sin tocar codigo):
1. Iniciar sesion.
2. No interactuar por 31 minutos.
3. Intentar abrir una pagina protegida.
4. Esperado:
   - redirige a Login por expiracion de cookie

Opcion de validacion rapida en laboratorio (temporal):
1. Cambiar temporalmente `SessionIdleTimeout` a 2 minutos en `Security/SecurityDefaults.cs`.
2. `dotnet build` + `dotnet run`, validar expiracion.
3. Revertir a 30 minutos.

## 7) CRUD operativo + auditoria

## 7.1 Oficios

1. Crear oficio nuevo.
2. Editar asunto/notas.
3. Eliminar oficio de prueba.
4. Esperado:
   - funciona sin error
   - AuditLog registra `CREATE/UPDATE/DELETE`

## 7.2 Casos

1. Crear caso dentro de oficio.
2. Editar campos (CURP/CCT/Folio/Estatus).
3. Eliminar caso.
4. Esperado:
   - reordenado de consecutivo correcto
   - AuditLog registra `CREATE/UPDATE/DELETE`

## 7.3 Evidencias

1. Subir imagen valida (<=8MB).
2. Subir PNG/BMP/TIFF y confirmar conversion a JPG.
3. Ver evidencia desde el sistema.
4. Eliminar evidencia.
5. Esperado:
   - registro `UPLOAD_EVIDENCE`, `VIEW_EVIDENCE`, `DELETE_EVIDENCE`
   - archivo fisico eliminado al borrar

## 7.4 Reportes

1. Abrir `/Reportes`.
2. Confirmar orden de secciones:
   - Oficios por tipo de correccion
   - Casos pendientes
   - Pendientes de evidencia
   - Oficios con evidencia incorrecta
3. Confirmar que `Pendientes de evidencia` muestra boton `Mostrar todos` cuando supera el limite de vista previa.
4. Confirmar que `Oficios con evidencia incorrecta` aparece junto a `Pendientes de evidencia` como tabla con columnas:
   - Oficio
   - Nivel
   - Tipo
   - CURP
5. Abrir `Casos pendientes` y confirmar que no existe columna `Estatus`.

## 7.5 Cifrado de datos y evidencias

1. Abrir un detalle de oficio/caso y confirmar que CURP, nombre, CCT, validador y observaciones se muestran legibles.
2. Verificar que no se vea texto `enc:v1:` en la aplicacion.
3. Abrir evidencia desde la aplicacion y confirmar que carga correctamente.
4. Abrir un archivo fisico de evidencia desde `C:\BitacoraEvidencias\Evidencias`.
5. Resultado esperado: el archivo fisico no debe abrir como imagen normal y debe iniciar con prefijo `BITACORA-ENC-V1`.
6. En SQLite, revisar una muestra de campos sensibles y confirmar formato `enc:v1:...`.

## 8) Evidencia protegida (no URL directa)

1. Copiar una ruta real de archivo en disco, por ejemplo:
   - `C:\BitacoraEvidencias\Evidencias\...\original\xxxx.jpg`
2. Intentar abrir por URL estatica tipo:
   - `https://127.0.0.1:5067/media/...`
   - `https://127.0.0.1:5067/Storage/...`
3. Esperado:
   - no accesible por URL directa (404/403)
4. Abrir evidencia solo desde:
   - endpoint autenticado `/Evidencias/View/{id}`

## 9) Validacion de DB (SQLite)

Usar DB Browser for SQLite o cliente SQL y ejecutar:

```sql
-- Eventos mas recientes
SELECT OccurredAtUtc, EventType, Username, IpAddress, EntityType, EntityId, NumeroOficio, Consecutivo, Curp, Cct, FolioCertificado, EvidenciasCount, Success
FROM AuditLogs
ORDER BY Id DESC
LIMIT 200;

-- Resumen por evento
SELECT EventType, COUNT(*) AS Total
FROM AuditLogs
GROUP BY EventType
ORDER BY Total DESC;

-- Estado de bloqueo de usuarios
SELECT Usuario, Activo, IntentosFallidos, BloqueadoHastaUtc, MustChangePassword
FROM UsuariosSistema
ORDER BY Usuario;

-- Evidencias registradas
SELECT e.Id, e.CasoCorreccionId, e.RutaArchivo, e.RutaMiniatura, e.FechaEvidencia
FROM EvidenciasFoto e
ORDER BY e.Id DESC
LIMIT 100;
```

## 10) Criterios de aprobacion (PASS/FAIL)

PASS si se cumple todo:
1. No hay acceso anonimo a paginas protegidas.
2. Login/logout correctos con trazabilidad.
3. Lockout funciona a los 5 intentos y dura ~30 min.
4. Revocacion de rol admin aplica a sesiones vivas.
5. CRUD de Oficio/Caso/Evidencia sin regresiones.
6. Evidencias solo accesibles por endpoint autenticado.
7. AuditLog contiene eventos obligatorios con IP, usuario, entidad y exito.

FAIL si ocurre cualquiera:
1. Evidencia accesible por URL estatica.
2. No se registra un evento obligatorio en AuditLog.
3. Lockout o timeout no se aplican.
4. Revocacion de rol no saca privilegios admin en una sesion activa.
5. Eliminacion no borra archivos fisicos.

## 11) Registro de ejecucion

Fecha: ____/____/______
Ejecuto: ______________________
Ambiente: ______________________
Resultado final: PASS / FAIL
Observaciones: _______________________________________________

