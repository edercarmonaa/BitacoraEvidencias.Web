# QA Funcional UI/UX + Seguridad (Manual)
Fecha: 2026-02-27  
Sistema: BitÃ¡cora Evidencias  
Ambiente sugerido: `https://127.0.0.1:5067`

## 1) PreparaciÃ³n
1. Levantar aplicaciÃ³n.
2. Abrir en navegador con certificado ya confiado.
3. Tener usuario `admin` operativo.
4. Tener al menos un usuario `capturista` (si no existe, crearlo desde Admin).
5. Contar con 2 imÃ¡genes de prueba:
   - `ok.jpg` (menor a 8 MB)
   - `grande.jpg` (mayor a 8 MB)

## 2) Resultado rÃ¡pido de humo (desde consola de esta sesiÃ³n)
- `https://127.0.0.1:5067/Login`: sin conexiÃ³n (app no levantada en esta sesiÃ³n).
- `https://127.0.0.1:5067/Login`: sin conexiÃ³n (app no levantada en esta sesiÃ³n).

## 3) Checklist ejecutable
Marca cada caso: `OK` / `FALLO` / `N/A`.

| ID | Caso | Pasos | Resultado esperado | Estado | ObservaciÃ³n |
|---|---|---|---|---|---|
| Q01 | Login vÃ¡lido | Entrar con `admin` | Acceso correcto a Inicio |  |  |
| Q02 | Login invÃ¡lido | Capturar contraseÃ±a errÃ³nea 1 vez | Mensaje de error visible, sin romper layout |  |  |
| Q03 | Lockout | Fallar login 5 veces | Usuario bloqueado 30 min + evento LOCKOUT |  |  |
| Q04 | Logout | Clic en `Cerrar sesiÃ³n` | Regresa a Login + evento LOGOUT |  |  |
| Q05 | MenÃº autenticado | Ver Login sin sesiÃ³n | No se muestra menÃº principal |  |  |
| Q06 | Breadcrumb | Ir a Oficios/Busqueda/Reportes/Admin | Breadcrumb visible y correcto |  |  |
| Q07 | Sticky topbar | Scroll vertical largo | Topbar permanece visible |  |  |
| Q08 | NavegaciÃ³n admin | Ir a `Usuarios` y `BitÃ¡cora` | Acceso permitido a Admin |  |  |
| Q09 | NavegaciÃ³n capturista | Entrar como capturista e ir a `/Admin/Users` | Acceso denegado (403/redirect) |  |  |
| Q10 | Oficios - crear | Nuevo oficio y guardar | Se crea registro + feedback correcto |  |  |
| Q11 | Oficios - editar | Editar oficio y guardar | Cambios persistidos + feedback correcto |  |  |
| Q12 | Oficios - eliminar | Eliminar oficio en pantalla Delete | Elimina oficio/casos/evidencias asociadas |  |  |
| Q13 | Casos - crear | Crear caso desde detalle de oficio | Consecutivo correcto + persistencia |  |  |
| Q14 | Casos - editar | Editar caso | Cambios persistidos |  |  |
| Q15 | Casos - eliminar | Eliminar caso | Elimina caso/evidencias y reordena consecutivos |  |  |
| Q16 | Evidencia - subir vÃ¡lida | Subir `ok.jpg` | Acepta y guarda evidencia |  |  |
| Q17 | Evidencia - lÃ­mite tamaÃ±o | Subir `grande.jpg` (>8 MB) | Rechaza con mensaje claro |  |  |
| Q18 | Evidencia - editar | Editar metadatos y/o reemplazar imagen | Cambios persistidos correctamente |  |  |
| Q19 | Evidencia - eliminar | Eliminar evidencia | Borra BD y archivo fÃ­sico |  |  |
| Q20 | Evidencia por endpoint | Ver evidencia desde UI | Carga solo autenticado por endpoint |  |  |
| Q21 | Bloqueo URL directa | Intentar abrir ruta fÃ­sica por URL | No accesible pÃºblicamente |  |  |
| Q22 | BÃºsqueda | Filtrar en BÃºsqueda y paginar | Resultados correctos + paginaciÃ³n estable |  |  |
| Q23 | Reportes | Abrir Reportes y usar `Actualizar` | MÃ©tricas/tablas cargan sin romper UI |  |  |
| Q24 | Tabla zebra/hover | Revisar Oficios, BÃºsqueda, Reportes, Usuarios, BitÃ¡cora | Fondo blanco + zebra + hover consistente |  |  |
| Q25 | Loading botones | En Create/Edit/Delete (3 mÃ³dulos) dar submit | BotÃ³n muestra `Guardando/Eliminando...` y evita doble clic |  |  |
| Q26 | Loading HTMX | Filtrar en BÃºsqueda/Oficios/BitÃ¡cora | Skeleton y atenuado durante carga |  |  |
| Q27 | UTF-8 UI | Recorrer menÃº, tÃ­tulos, botones, breadcrumb | No aparece texto con codificaciÃ³n daÃ±ada (ejemplo: caracteres corruptos). |  |  |
| Q28 | AuditLog LOGIN_FAIL | Provocar fallo de login | Evento `LOGIN_FAIL` registrado |  |  |
| Q29 | AuditLog CRUD | Ejecutar CREATE/UPDATE/DELETE de oficio/caso | Eventos correspondientes registrados |  |  |
| Q30 | AuditLog evidencia | Subir/ver/borrar evidencia | `UPLOAD/VIEW_EVIDENCE/DELETE_EVIDENCE` registrados |  |  |
| Q31 | AuditLog borrado definitivo | Eliminar oficio/caso con evidencias | Rastro conserva oficio/consecutivo/sufijos o hashes de claves/#img/quien/cuando |  |  |
| Q32 | Timeout sesiÃ³n | Dejar inactivo 30 min y operar | Redirige a login por expiraciÃ³n |  |  |
| Q33 | Reportes - orden | Abrir `/Reportes` | Orden: Oficios por tipo, Casos pendientes, Pendientes de evidencia, Oficios con evidencia incorrecta |  |  |
| Q34 | Reportes - pendientes evidencia | Forzar mas de 5 pendientes | Boton `Mostrar todos` visible y navega al detalle |  |  |
| Q35 | Reportes - casos pendientes | Abrir detalle de Casos pendientes | No aparece columna `Estatus` |  |  |
| Q36 | Reportes - evidencia incorrecta | Abrir `/Reportes` | Panel junto a Pendientes de evidencia con columnas Oficio/Nivel/Tipo/CURP |  |  |
| Q37 | Cifrado - UI | Abrir oficio/caso despues de migracion | No aparece `enc:v1:`; datos se muestran legibles |  |  |
| Q38 | Cifrado - archivos | Abrir archivo fisico de evidencia desde disco | No abre como imagen normal; contiene prefijo `BITACORA-ENC-V1` |  |  |
| Q39 | Cifrado - llave | Arrancar app publicada | Consola muestra `Llave de cifrado: configurada` |  |  |

## 4) Criterio de cierre
- `0` fallos crÃ­ticos en autenticaciÃ³n/autorizaciÃ³n/bitÃ¡cora.
- `0` fallos crÃ­ticos en creaciÃ³n/ediciÃ³n/eliminaciÃ³n.
- Si hay fallos: registrar ID y evidencia (captura + hora + usuario).

## 5) Registro de hallazgos
| ID Caso | Severidad (Alta/Media/Baja) | DescripciÃ³n | Evidencia | AcciÃ³n propuesta |
|---|---|---|---|---|
|  |  |  |  |  |

