# Rendimiento de Busqueda

## Objetivo

Validar el impacto del cambio aplicado en `Busqueda`:
- modo rapido por defecto (`prefijo`, `valor%`)
- modo opcional `Busqueda amplia (contiene)` (`%valor%`)
- orden por `FechaOficio DESC`

## Fecha y entorno

- Fecha de prueba: 25/02/2026
- Proyecto: `BitacoraEvidencias.Web`
- Base evaluada: `Data/bitacora-evidencias.dev.db`
- Total de casos: `8`

## Metodo

1. Se seleccionaron valores reales de BD para pruebas de:
- Numero de oficio
- CURP
- Folio

2. Para cada campo se midio la misma consulta en dos modos:
- `prefijo`: `LIKE 'valor%'`
- `contiene`: `LIKE '%valor%'`

3. Configuracion de medicion:
- 5 ejecuciones de calentamiento
- 60 ejecuciones medidas por modo
- metrica: `AvgMs`, `P95Ms`, `MinMs`, `MaxMs`
- `LIMIT 20`, `ORDER BY FechaOficio DESC, Consecutivo DESC`

## Llaves de prueba (mascaradas)

- Oficio (prefijo): `SEV/DJ/D`
- CURP (prefijo): `TIRR84`
- Folio (prefijo): `CE302103`

## Resultados

| Campo  | Modo      | AvgMs | P95Ms | MinMs | MaxMs | Filas |
|--------|-----------|------:|------:|------:|------:|------:|
| Oficio | Prefijo   | 0.107 | 0.150 | 0.083 | 0.512 | 7 |
| Oficio | Contiene  | 0.084 | 0.091 | 0.081 | 0.112 | 7 |
| CURP   | Prefijo   | 0.082 | 0.091 | 0.079 | 0.110 | 1 |
| CURP   | Contiene  | 0.081 | 0.089 | 0.077 | 0.102 | 1 |
| Folio  | Prefijo   | 0.099 | 0.109 | 0.084 | 0.600 | 7 |
| Folio  | Contiene  | 0.122 | 0.164 | 0.082 | 1.493 | 7 |

## Planes de consulta (resumen)

- En esta BD de prueba, SQLite reporta `SCAN` para varias variantes.
- Conjunto de datos pequeno (`8` casos): diferencias de tiempo no son estadisticamente robustas.
- Aun asi, en Folio se observa mejora de `prefijo` sobre `contiene` en promedio y p95.

## Interpretacion tecnica

1. El cambio funcional queda correcto:
- Por defecto se privilegia busqueda rapida.
- Usuario conserva opcion de busqueda amplia cuando la necesita.

2. En este entorno de prueba:
- El beneficio no es concluyente para todos los campos por tamano reducido de muestra.

3. Para evidencia operativa real:
- Repetir medicion en BD con volumen mayor (produccion historica).

## Recomendaciones siguientes (opcional)

1. Medir en una copia de BD con mayor volumen y registrar p95/p99.
2. Considerar indices compuestos orientados al orden real de consulta.
3. Revisar expresiones que fuerzan `SCAN` en campos opcionales.
