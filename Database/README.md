# Scripts de base de datos

El esquema de `IVC` es preexistente y lo comparten esta app, el legado y Power BI: no hay migraciones de EF Core. Lo que la app necesita además del esquema se versiona acá, numerado, y se ejecuta a mano primero en `IVC_TEST` y después en producción.

| Script | Qué crea | Cuándo ejecutarlo |
|---|---|---|
| `001_CambiosAuditoria.sql` | Tabla `CambiosAuditoria` e índices (idempotente). | Una vez por base, antes del primer arranque de la app. Ejecutado en `IVC_TEST` el 10/11/2026. |
| `002_vw_PowerBI_Redeterminaciones.sql` | Vistas `vw_PBI_Redeterminaciones`, `vw_PBI_Reliquidaciones` y `vw_PBI_Pagos` con las reglas R1–R7 (`CREATE OR ALTER`, idempotente). | La app no las usa; las lee el Power BI y el test de paridad `Tests/Repositories/RedeterminacionesParidadTests.cs`. Desplegadas en producción el 01/11/2026. Si cambia una regla, se cambian la vista y `Application/Redeterminaciones/ReglasRedeterminaciones` juntas. |
| `esquema-columnas.sql` | Nada: consulta de referencia que lista tablas, columnas, tipos y nulabilidad. | Para comparar el esquema real con `Data/Configurations/` cuando se sospeche una diferencia. |

Los scripts no fijan la base con `USE`: conectarse a la base correcta antes de ejecutarlos. `002` viene del repo legado (`Database/vw_PowerBI_Redeterminaciones.sql`, última versión del 01/11/2026) sin cambios de contenido.
