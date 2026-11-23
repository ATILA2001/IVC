# IVC — Instituto de la Vivienda

## 1) Propósito

`IVC` es la aplicación interna (Blazor Server + Radzen, .NET 10) para la gestión de obras, autorizantes, certificados, redeterminaciones de precios, legítimos abonos y formulación presupuestaria. Reemplaza a la aplicación WebForms del repo [`Instituto-de-la-Vivienda`](../Instituto-de-la-Vivienda), cuyo análisis y plan de migración están en `Docs/migracion-net10.md` de ese repo (rama `docs/plan-migracion-net10`).

### Páginas

Las trece páginas se declaran en `Components/PaginasIvc.cs` y sus URL son las registradas en `Auth.Web`. Todas son grillas sobre `GridPageBase` con búsqueda, filtros persistentes, export a Excel e historial de auditoría por fila.

| URL | Página | Quién la ve y qué puede hacer |
|---|---|---|
| `/obras` | Obras | Áreas del usuario (admin y Redeterminaciones ven todas); alta, edición y baja en ventana con la planificación abierta; columnas financieras ocultas a Redeterminaciones. |
| `/autorizantes` | Autorizantes | Según `perms_json`; filas reales más una por redeterminación; inline expediente y estado; ventana y baja con la planificación abierta. |
| `/certificados` | Certificados | Según `perms_json`; certificados más reliquidaciones calculadas; inline expediente, monto y ejecución física; ventana y baja con la planificación abierta. |
| `/redeterminaciones` | Redeterminaciones | Cross-area; inline etapa, expediente y usuario; filtros de control de estados y días en buzón; escribe quien tiene el permiso de redeterminaciones (todos menos Secretaría). |
| `/legitimos` | Legítimos abonos | Áreas del usuario; inline expediente y monto; ventana y baja con la planificación abierta. |
| `/formulacion` | Formulación | Áreas del usuario (admin todas); una fila por obra con los tres años del ciclo; alta, edición y baja en ventana con la formulación abierta; PPI y techo sólo los edita el admin. |
| `/proyectos`, `/movimientos` | Proyectos y Movimientos de gestión | Catálogos con edición inline según `perms_json`; la obra se elige sólo en el alta. |
| `/barrios`, `/empresas`, `/lineas-gestion`, `/lineas-gestion-ff` | Catálogos | Edición inline según `perms_json`; nombres únicos. |
| `/admin/configuracion` | Configuración | Sólo administradores: interruptores de planificación y formulación (`ABM_PLANI`) y override de planificación por usuario. |

La autenticación **no** se resuelve en este proyecto: IVC es aplicación cliente del SSO central [`Auth.Web`](../Auth.Web), con la misma mecánica que SAF y SAI. Comparte la cookie de identidad mediante DataProtection apuntando a la base de `Auth.Web` y lee los permisos por página desde el claim `perms_json`. Ver `Extensions/IvcBuilderExtensions.cs` y `Security/`.

Al redirigir al login, IVC manda el `clientId` configurado en `AuthWeb:ClientId`; si está vacío, `Auth.Web` deduce la aplicación por el `returnUrl`. No hardcodear un `clientId` de fallback: si no coincide exacto con el registrado en `Auth.Web`, el login corta con `invalid_client`.

## 2) Estructura del proyecto

Misma metodología de capas que `SAF` y `Auth.Web`.

```
Application/                 # Reglas puras y DTOs por feature (sin EF ni UI; 100 % testeable)
├─ Common/                   # Diff de auditoría, atributos Export/Audit/Restore, validaciones
├─ Identidad/                # UsuarioIvc, EstadoPlanificacion, IvcAreaIds y ReglasPermisosIvc (ex UserHelper)
├─ Catalogos/                # ViewModels y validadores de Barrios, Empresas, Líneas, Líneas FF, Proyectos y Movimientos
├─ Obras/                    # ObraViewModel, ObraValidator e IndicadoresObra (fórmulas de CalculoObraNegocioEF)
└─ Redeterminaciones/        # ReglasRedeterminaciones (R1–R7), MapeoEstadoRedet, PropiedadesPago, BuzonEtapaMatcher, SeguimientoBuzon, ViewModels y validadores

Components/                  # UI Blazor (.razor + code-behind .razor.cs)
├─ Account/                  # Login / AccessDenied (delegan en Auth.Web)
├─ Layout/                   # MainLayout: menú armado desde PaginasIvc + perms_json
├─ Pages/                    # Páginas routables, una carpeta por feature (Admin, Catalogos, Obras, ...)
├─ Shared/                   # Toolbar, pie de totales, historial de auditoría, etc.
├─ GridPageBase.cs           # Base de páginas con grilla (carga, edición, auditoría, export)
├─ PermissionPageBase.cs     # Base de páginas con control de permisos
└─ PaginasIvc.cs             # Catálogo de páginas (URL = la registrada en Auth.Web)

Data/                        # EF Core
├─ Entities/                 # Entidades mapeadas a las tablas existentes de IVC (una clase por tabla)
├─ Configurations/           # Fluent API por módulo: tabla, columnas, precisión, claves, navegaciones
├─ IvcDbContext.cs           # Base IVC (lectura y escritura). SIN migraciones: esquema preexistente
└─ DataProtectionDbContext.cs# Apunta a la base de Auth.Web (key ring compartido)

Database/                    # Scripts versionados: 001 tabla de auditoría, 002 vistas Power BI, esquema de referencia
Docs/                        # Documentación funcional (cálculo de redeterminaciones)
Extensions/                  # Program.cs modular (builder y pipeline)
Repositories/                # Acceso a datos (Abstractions / Implementations)
Services/                    # Lógica de aplicación (Abstractions / Implementations)
Security/                    # Cookie compartida, claims de admin, validación del returnUrl
Shared/                      # Helpers compartidos (notificaciones, estado de grillas)
Tests/                       # MSTest + bUnit (Application, Components, Repositories, Security, Services)
```

## 3) Convenciones

- **Capas:** `Components` → `Services` → `Repositories` → `Data`. Los `Services` devuelven DTOs de `Application/<Feature>/Dtos/`; nunca exponen tipos de UI hacia abajo.
- **Code-behind:** cada página es `Pagina.razor` + `Pagina.razor.cs` (`partial class`).
- **DI:** registro explícito en `Extensions/IvcBuilderExtensions.cs` (repos `Transient`, services `Scoped`). Los repositorios crean su propio `IvcDbContext` por operación con `IDbContextFactory`.
- **Base de datos:** el esquema IVC es preexistente y lo comparten el legado, SAF y Power BI. `IvcDbContext` no genera migraciones; las tablas nuevas de la app se crean con scripts numerados en `Database/` (ejecutar primero en `IVC_TEST`). Los nombres de tabla y columna se declaran explícitos en `Data/Configurations/`; `Database/esquema-columnas.sql` lista el esquema real para compararlo con el mapeo. Las bajadas externas (`PASES_SADE`, `GEDO`, `DEVENGADOS`) se mapean sin clave y sólo lectura. Autorizantes, Certificados y Redeterminaciones se vinculan por `CODIGO_AUTORIZANTE`, que no es clave primaria: esa relación se resuelve en los servicios, no como navegación.
- **Páginas:** la URL de cada página se declara una sola vez en `Components/PaginasIvc.cs` y es la misma que se registra en `Auth.Web` para los permisos.
- **Identidad y permisos:** `perms_json` decide a qué páginas entra el usuario (`PermissionPageBase`). Las reglas funcionales de edición que `perms_json` no expresa (rol, área, interruptores de `ABM_PLANI`) viven en `Application/Identidad/ReglasPermisosIvc` como funciones puras sobre un `UsuarioIvc`; `IUsuarioActual` construye ese usuario una vez por circuito desde los claims y la base (áreas por `AREAS.AuthAreaId`, alta y nombre en `UsuariosVinculados`) y `IPermisosIvc` evalúa las reglas con el estado vigente. Los campos inline de las grillas los edita cualquier usuario autenticado; con la planificación cerrada se bloquean sólo alta, ventana y baja (dictamen del área del 04/11/2026). Las reglas se verifican en cada handler que escribe, además de ocultar controles.
- **Auditoría:** toda alta/edición/baja manual queda registrada en `CambiosAuditoria` (quién, cuándo, campo, valor anterior y nuevo).
- **Catálogos:** Barrios, Empresas, Líneas de gestión, Líneas FF, Proyectos y Movimientos son grillas con edición inline sobre `GridPageBase`; alta, edición y baja se habilitan por `perms_json` (el legado no aplicaba las reglas de planificación en estos ABM). Los nombres de Barrios, Empresas y Líneas se exigen únicos. La obra de un proyecto o de un movimiento se elige sólo en el alta (un proyecto por obra, como la restricción única de `BD_PROYECTOS`). Un borrado que la base rechaza por estar en uso vuelve como mensaje al usuario (el legado lo tragaba). El "Autorizado nuevo" de Movimientos se calcula como en el legado (autorizado 2025 del proyecto más todos los movimientos de la obra) y el test de integración lo compara fila por fila con la consulta original. Los montos siguen la regla del legado: positivos con hasta dos decimales.
- **Obras:** la grilla muestra las obras de las áreas del usuario (administradores y Redeterminaciones ven todas) con los indicadores del legado calculados en `Application/Obras/IndicadoresObra` sobre los agregados de autorizantes, certificados (vinculados por `CODIGO_AUTORIZANTE`) y legítimos; las columnas financieras se ocultan al área Redeterminaciones también en el export. Alta, edición y baja van en ventana y sólo con la planificación abierta; el área se elige en el alta (restringida a las del usuario) y no se cambia al editar. El test de integración compara los indicadores de todas las obras con las mismas fórmulas escritas en SQL.
- **Redeterminaciones (Autorizantes, Certificados, Legítimos y Redeterminaciones):** las reglas de cálculo (R1–R7, fijadas con el área entre el 22/09 y el 28/10/2026) viven en `Application/Redeterminaciones/ReglasRedeterminaciones` como funciones puras, portadas 1:1 del legado con sus 24 tests; deben dar lo mismo que la vista `vw_PBI_Redeterminaciones` del Power BI. Las grillas mezclan filas reales con filas calculadas: Autorizantes agrega una fila por redeterminación (código de redeterminación, estado general de la etapa y monto redeterminado) y Certificados agrega las reliquidaciones que cada redeterminación genera sobre los certificados posteriores al salto. Los datos externos (último pase SADE, devengado SIGAF y GEDO) se leen en bloque por expediente en `IDatosExternosRepository`; el SIGAF se prorratea entre los pagos del mismo expediente en Certificados y, por separado, entre los legítimos que comparten expediente en Legítimos (R4: legítimos y certificados no se mezclan; hotfix PR #19 del legado, 19/11/2026). El expediente del legítimo es opcional (se carga en planificación antes de tenerlo y queda NO INICIADO). Edición inline sin depender de la planificación: expediente y estado en Autorizantes; expediente, monto y ejecución física en Certificados (en las reliquidaciones sólo el expediente, que se guarda en `EXPEDIENTES_RELIQ`); expediente y monto en Legítimos; etapa, expediente y usuario en Redeterminaciones. Alta, edición completa y baja van en ventana y exigen la planificación abierta (Redeterminaciones: el permiso de redeterminaciones, todos menos Secretaría). Visibilidad como en el legado: Autorizantes la ven completa admin y Redeterminaciones (sin mes base ni SADE para esta área); en Certificados y Legítimos sólo el admin ve todo; Redeterminaciones es cross-area. La base manda en lo que ya validaba por triggers: `TRG_Autorizantes_Insert` asigna el código del autorizante (la app no lo envía y lo relee), `trg_Autorizante_CheckMonto` y `trg_Certificado_CheckMonto` rechazan montos inconsistentes y su mensaje llega al usuario; esas tablas se declaran con `HasTrigger` porque EF Core no puede usar `OUTPUT` en tablas con triggers. La página de Redeterminaciones conserva los filtros "control de estados" (buzón SADE que no corresponde a la etapa, tabla en `BuzonEtapaMatcher`) y "días en buzón" (verde < 7, naranja 7–14, rojo > 14; sin días en las etapas terminales).
- **Formulación:** una fila lógica por obra que agrupa sus (hasta) tres filas `FORMULACION` del ciclo vigente (año base de `Formulacion:AnioBase` en appsettings, hoy 2027, y los dos siguientes; `Application/Formulacion/FormulacionCiclo`). Los campos compartidos (mes base, PPI, techo, unidad y valor de medida, prioridad, descripción, fechas, observaciones) son idénticos en las tres filas y se escriben iguales; los montos son uno por año y, al guardar, el año sin monto se elimina y el año con monto se inserta o actualiza (misma regla que `GuardarPivot` del legado). Alta, edición y baja van en ventana y exigen la formulación abierta (administrador siempre; Redeterminaciones y Secretaría nunca); el alta sólo ofrece obras del usuario sin formulación en el ciclo. El administrador ve todas las obras y es el único que ve y edita el PPI y que edita el techo (el panel oculto del legado); el resto ve las de sus áreas, conserva el PPI y el techo existentes al guardar, y su export e historial salen sin PPI, como su grilla (en el legado el export lo incluía). Las columnas de monto llevan el año del ciclo en el título, también en el export.
- **Cultura:** `es-AR` fija (coma decimal, `dd/MM/yyyy`).
- **Inglés** para nombres de capas/carpetas; el dominio funcional va en español.
- **Commits:** Conventional Commits (`feat`, `fix`, `refactor`, `docs`, `chore`). Ramas: `dev` integración, `main` producción, `feature/*`, `fix/*`, `hotfix/*`.

### Diferencias deliberadas con SAF

SAF es la plantilla, pero IVC se aparta en cuatro puntos. Se mantienen a propósito (decisión del 09/11/2026):

1. **`Data/Configurations/` con `IEntityTypeConfiguration`.** SAF, SAI y Auth.Web escriben el Fluent API dentro del `OnModelCreating` del contexto. IVC mapea 27 tablas preexistentes con nombres de columna explícitos, y el contexto quedaría ilegible: el mapeo va en un archivo por módulo y el contexto lo aplica con `ApplyConfigurationsFromAssembly`.
2. **`Components/PaginasIvc.cs`.** SAF repite la lista de páginas en `MainLayout.razor` y en `Home.razor`. IVC la declara una sola vez (URL, título, descripción, ícono) y el menú y el inicio la consumen; la URL es la misma que se registra en Auth.Web.
3. **Tests de integración opcionales** (`TestCategory=Integracion`). Leen la base real sólo si existe `IVC_TEST_CONNECTION`; sin la variable quedan `Inconclusive`. SAF no tiene tests contra base.
4. **Cultura `es-AR` fija** con `RequestLocalization`. SAF sólo fija la cultura dentro de `AuditoriaDiff`; IVC parsea y muestra montos con coma decimal en toda la app.

Además, por decisión de la fase 0: un solo `IvcDbContext` database-first sin migraciones (SAF tiene base propia con migraciones), tablas nuevas por scripts en `Database/`, y CI en GitHub Actions.

## 4) Requisitos y ejecución

- SDK de .NET 10.
- El repo `Auth.Web` clonado al lado de este (`../Auth.Web`): el proyecto linkea `Security/SharedCookieTicketDto.cs` para que la estructura de la cookie no pueda divergir.
- SQL Server con la base `IVC_TEST` (desarrollo) y lectura sobre la base de `Auth.Web`.

Cadenas de conexión y configuración en desarrollo por user secrets (nunca en el repo):

```bash
dotnet user-secrets set "ConnectionStrings:IvcConnection" "..."
dotnet user-secrets set "ConnectionStrings:AuthWebConnection" "..."
dotnet user-secrets set "SharedCookie:Name" "..."
dotnet user-secrets set "SharedCookie:ApplicationName" "..."
dotnet user-secrets set "AuthWeb:BaseUrl" "https://localhost:7107"
dotnet user-secrets set "AuthWeb:ClientId" "..."
```

```bash
dotnet build IVC.sln
dotnet test Tests/Tests.csproj
dotnet run
```

Los tests de la categoría `Integracion` leen una fila de cada tabla contra una base IVC real para validar el mapeo, y verifican los datos que las reglas de permisos dan por sentados (áreas 16 y 19 mapeadas a Auth.Web, una sola fila en `ABM_PLANI`, largo de `UsuariosVinculados.Nombre`). Corren sólo si existe la variable de entorno `IVC_TEST_CONNECTION` (apuntar siempre a `IVC_TEST`); sin ella quedan `Inconclusive` y el CI no los necesita:

```powershell
$env:IVC_TEST_CONNECTION = "<cadena a IVC_TEST>"; dotnet test Tests/Tests.csproj --filter "TestCategory=Integracion"
```

Antes del primer arranque contra una base, ejecutar `Database/001_CambiosAuditoria.sql`. Las vistas de Power BI (`Database/002_vw_PowerBI_Redeterminaciones.sql`) no las necesita la app, pero sí los tests de paridad de redeterminaciones; en producción ya están desplegadas desde el 01/11/2026. Ver `Database/README.md`. La app redirige al login de `Auth.Web` si el usuario no está autenticado. `/healthz` responde el estado de la conexión a IVC (anónimo).

## 5) Publicación (IIS / Web Deploy)

Igual que SAF: perfil `Properties/PublishProfiles/*.pubxml` fuera de git, con `IsTransformWebConfigDisabled=true` y `SkipExtraFilesOnServer=true`. La configuración real del sitio (connection strings, `SharedCookie__*`, `AuthWeb__BaseUrl`, `AuthWeb__ClientId`) vive como variables de entorno en el `web.config` del servidor y **no debe pisarse** en los deploys.

## 6) CI

`.github/workflows/ci.yml` compila y corre los tests en cada PR y push a `dev` y `main`. Como el proyecto linkea un archivo de `Auth.Web`, el workflow también clona ese repo; requiere el secreto `AUTH_WEB_READ_TOKEN` con permiso de lectura sobre `ATILA2001/Auth.Web`. Los tests de integración quedan `Inconclusive` en CI porque no hay base.

El workflow está **deshabilitado** en GitHub desde el 09/11/2026 porque el secreto no está cargado. Para reactivarlo: crear un token de sólo lectura sobre `Auth.Web`, cargarlo como secreto `AUTH_WEB_READ_TOKEN` del repo `IVC` y ejecutar `gh workflow enable CI`.

## 7) Seguridad

Lo que está en pie y por qué; las decisiones que se tomaron a propósito van al final.

- **Autenticación delegada.** Nadie se loguea en IVC: la cookie la emite `Auth.Web` y acá sólo se lee (`SharedCookieTicketDataFormat`, key ring compartido por DataProtection). La cookie es `Secure`, `HttpOnly`, `SameSite=None` (necesaria para el SSO entre sitios) y expira a las 8 horas deslizantes. La política global exige usuario autenticado; sólo `/healthz`, `/Error`, `/Account/*` son anónimos.
- **Autorización en dos niveles.** `perms_json` decide a qué página entra el usuario y qué botones ve (`PermissionPageBase`); las reglas funcionales (rol administrador, área, interruptores de `ABM_PLANI`) se evalúan de nuevo en cada servicio antes de escribir, así que un botón habilitado por error en la UI no alcanza para modificar datos. `perms_version` se revalida contra `Auth.Web` cada 5 minutos y, si cambió, el usuario vuelve al login para renovar la cookie (fail-open: si `Auth.Web` no responde, se sigue con la cookie vigente, como en el legado).
- **Transporte y cabeceras.** HTTPS obligatorio (redirección 308, HSTS de un año con subdominios), `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-XSS-Protection: 0`; antiforgery en el pipeline. Son las mismas de SAF, SAI y el legado.
- **Redirecciones.** El `returnUrl` que se reenvía al login central tiene que ser una ruta relativa de esta app (`Security/ReturnUrls`); otro host, esquema o caracteres de control vuelven al inicio. `Auth.Web` lo valida de nuevo contra sus `AllowedReturnUrls`. El logout sólo redirige a la base configurada en `AuthWeb:BaseUrl`.
- **Datos.** Todo el acceso va por EF Core parametrizado; el SQL crudo existe sólo en los tests de paridad. Los montos y largos se validan en `Application/*Validator` y la base conserva sus triggers de consistencia. Toda escritura manual queda en `CambiosAuditoria`.
- **Secretos.** Nada sensible en el repo: `appsettings.json` trae las claves vacías, desarrollo usa user secrets y producción variables de entorno en el `web.config`. Los logs no incluyen cadenas de conexión ni cookies (el nivel de EF Core es `Warning`).
- **Errores.** Fuera de desarrollo, la página de error muestra sólo un código de referencia que coincide con el `TraceIdentifier` del log.

Decisiones deliberadas, no omisiones:

- `AllowedHosts` queda en `*` porque no se conoce la topología de publicación (proxy, binding por IP); fijarlo sin esa información puede dejar el sitio sin responder. Ajustarlo en el `web.config` del servidor cuando se defina el host.
- No hay `Content-Security-Policy`: Blazor Server y Radzen usan scripts y estilos inline y una CSP restrictiva rompe la UI; una permisiva no aporta. Mismo criterio que SAF.
- Sin rate limiting ni bloqueo por IP: la app es interna y el login vive en `Auth.Web`.
- Advertencias NuGet `NU1902/NU1903` (`System.Security.Cryptography.Pkcs` 9.0.4 transitivo y `AngleSharp` 1.2.0, este último sólo en tests): heredadas de SAF; se resuelven actualizando paquetes, lo que requiere autorización expresa.

## 8) Rendimiento

- **Lectura en bloque.** Cada grilla se arma con un número fijo de consultas (una por tabla involucrada más los agregados), nunca una por fila. Los datos externos (SADE, SIGAF, ACDIR) se leen por lotes de 800 expedientes. El test de integración `Tests/Repositories/RendimientoIntegracionTests.cs` cuenta las consultas de la carga completa de las ocho grillas con un interceptor de EF Core, exige que no crezcan con las filas (seis fijas más un lote de 800 por fuente externa; un N+1 falla acá antes que en producción) e informa filas, milisegundos totales y en SQL y la consulta más lenta por consola.
- **Carga progresiva.** `GridPageBase` muestra el primer lote de 100 filas y completa el resto en segundo plano en lotes de 500, con el export bloqueado hasta terminar. Las páginas de hoy cargan todo de una vez porque el volumen lo permite; cualquier grilla que crezca puede sobreescribir `ObtenerLoteAsync` / `CompletarAsync` sin tocar la UI.
- **Medición de referencia** (23/11/2026, `IVC_TEST` por VPN, segunda pasada en caliente). `IVC_TEST` tiene un volumen comparable al de producción (5.003 pagos entre certificados y reliquidaciones, 650 autorizantes con sus redeterminaciones, 264 obras):

  | Grilla | Filas | Consultas | Total | De eso, SQL |
  |---|---|---|---|---|
  | Certificados | 5.003 | 8 | 2,7 s | 0,9 s |
  | Autorizantes | 650 | 6 | 2,2 s | 0,6 s |
  | Obras | 264 | 6 | 0,26 s | 0,17 s |
  | Legítimos | 177 | 3 | 0,27 s | 0,22 s |
  | Redeterminaciones | 26 | 3 | 0,15 s | 0,15 s |
  | Formulación, Proyectos, Movimientos | ≤ 244 | 1 | < 0,1 s | < 0,05 s |

  En las cuatro grillas de redeterminaciones la consulta más lenta es siempre la de `PASES_SADE` (0,3 a 0,6 s por lote de 800 expedientes): es una bajada externa sin índice por expediente. Si el tiempo molesta, el primer candidato es un índice en `PASES_SADE (EXPEDIENTE) INCLUDE (BUZON DESTINO, FECHA ULTIMO PASE)`, decisión del DBA. El resto del tiempo de Certificados y Autorizantes es la materialización de las filas y el cálculo en memoria de redeterminaciones y reliquidaciones, que crece en proporción a las filas. Para comparar con producción, correr el test de rendimiento contra una copia restaurada en `IVC_TEST` y mirar la misma tabla.
- **Contexto por operación.** Los repositorios crean y descartan su `IvcDbContext` en cada llamada (`IDbContextFactory`), con `AsNoTracking` en las lecturas y reintentos ante cortes transitorios. Al arrancar, un warmup abre la conexión y compila el modelo para que el primer usuario no lo pague.

## 9) Documentación y scripts

- `Docs/calculo-redeterminaciones.html`: las reglas R1–R7, las fórmulas, el ejemplo numérico, las vistas de Power BI y dónde vive cada pieza en este repo. Es el documento que se comparte con el área.
- `Database/001_CambiosAuditoria.sql`: tabla e índices de auditoría (idempotente; ejecutar una vez por base).
- `Database/002_vw_PowerBI_Redeterminaciones.sql`: las tres vistas del Power BI con las mismas reglas que la app; tienen que coincidir con `Application/Redeterminaciones/ReglasRedeterminaciones` y el test de paridad lo verifica.
- `Database/esquema-columnas.sql`: consulta de referencia para comparar el esquema real con el mapeo EF.
- El análisis de la migración y las decisiones por fase están en `Docs/migracion-net10.md` del repo legado (rama `docs/plan-migracion-net10`).

## 10) Corte a producción (pendiente)

Decisión del 03/11/2026: big-bang, cuando todas las páginas estén validadas por los usuarios en `IVC_TEST`. Lista de tareas, en orden:

1. Pruebas manuales de los usuarios en `IVC_TEST` (obras, autorizantes, certificados, legítimos, redeterminaciones, formulación, export y auditoría).
2. En `Auth.Web` de producción: alta del cliente, las 13 `Pages`, `AreaPagePermissions`, `AreaRoutes` y `UserApplications`, con el mismo script que se usó en test (fuera del repo), e incrementar `PermissionVersion`.
3. Sitio IIS de producción con su `web.config` de variables (connection strings, `SharedCookie__*`, `AuthWeb__*`) y perfil de publicación local.
4. Ejecutar `Database/001_CambiosAuditoria.sql` en la base de producción (las vistas 002 ya están).
5. Legado en sólo lectura una o dos semanas (quitando permisos de escritura en `Auth.Web`), luego apagado; repo `Instituto-de-la-Vivienda` archivado con un README que apunte acá.
