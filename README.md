# IVC — Instituto de la Vivienda

## 1) Propósito

`IVC` es la aplicación interna (Blazor Server + Radzen, .NET 10) para la gestión de obras, autorizantes, certificados, redeterminaciones de precios, legítimos abonos y formulación presupuestaria. Reemplaza a la aplicación WebForms del repo [`Instituto-de-la-Vivienda`](../Instituto-de-la-Vivienda), cuyo análisis y plan de migración están en `Docs/migracion-net10.md` de ese repo.

La autenticación **no** se resuelve en este proyecto: IVC es aplicación cliente del SSO central [`Auth.Web`](../Auth.Web), con la misma mecánica que SAF y SAI. Comparte la cookie de identidad mediante DataProtection apuntando a la base de `Auth.Web` y lee los permisos por página desde el claim `perms_json`. Ver `Extensions/IvcBuilderExtensions.cs` y `Security/`.

Al redirigir al login, IVC manda el `clientId` configurado en `AuthWeb:ClientId`; si está vacío, `Auth.Web` deduce la aplicación por el `returnUrl`. No hardcodear un `clientId` de fallback: si no coincide exacto con el registrado en `Auth.Web`, el login corta con `invalid_client`.

## 2) Estructura del proyecto

Misma metodología de capas que `SAF` y `Auth.Web`.

```
Application/                 # Reglas puras y DTOs por feature (sin EF ni UI; 100 % testeable)
├─ Common/                   # Diff de auditoría, atributos Export/Audit/Restore, validaciones
├─ Identidad/                # UsuarioIvc, EstadoPlanificacion, IvcAreaIds y ReglasPermisosIvc (ex UserHelper)

Components/                  # UI Blazor (.razor + code-behind .razor.cs)
├─ Account/                  # Login / AccessDenied (delegan en Auth.Web)
├─ Layout/                   # MainLayout: menú armado desde PaginasIvc + perms_json
├─ Pages/                    # Páginas routables, una carpeta por feature
├─ Shared/                   # Toolbar, pie de totales, historial de auditoría, etc.
├─ GridPageBase.cs           # Base de páginas con grilla (carga, edición, auditoría, export)
├─ PermissionPageBase.cs     # Base de páginas con control de permisos
└─ PaginasIvc.cs             # Catálogo de páginas (URL = la registrada en Auth.Web)

Data/                        # EF Core
├─ Entities/                 # Entidades mapeadas a las tablas existentes de IVC (una clase por tabla)
├─ Configurations/           # Fluent API por módulo: tabla, columnas, precisión, claves, navegaciones
├─ IvcDbContext.cs           # Base IVC (lectura y escritura). SIN migraciones: esquema preexistente
└─ DataProtectionDbContext.cs# Apunta a la base de Auth.Web (key ring compartido)

Database/                    # Scripts versionados (tablas propias de la app, vistas Power BI)
Extensions/                  # Program.cs modular (builder y pipeline)
Repositories/                # Acceso a datos (Abstractions / Implementations)
Services/                    # Lógica de aplicación (Abstractions / Implementations)
Security/                    # Cookie compartida + claims de admin
Shared/                      # Helpers compartidos (notificaciones, estado de grillas)
Tests/                       # MSTest + bUnit (Application, Components, Services)
```

## 3) Convenciones

- **Capas:** `Components` → `Services` → `Repositories` → `Data`. Los `Services` devuelven DTOs de `Application/<Feature>/Dtos/`; nunca exponen tipos de UI hacia abajo.
- **Code-behind:** cada página es `Pagina.razor` + `Pagina.razor.cs` (`partial class`).
- **DI:** registro explícito en `Extensions/IvcBuilderExtensions.cs` (repos `Transient`, services `Scoped`). Los repositorios crean su propio `IvcDbContext` por operación con `IDbContextFactory`.
- **Base de datos:** el esquema IVC es preexistente y lo comparten el legado, SAF y Power BI. `IvcDbContext` no genera migraciones; las tablas nuevas de la app se crean con scripts numerados en `Database/` (ejecutar primero en `IVC_TEST`). Los nombres de tabla y columna se declaran explícitos en `Data/Configurations/`; `Database/esquema-columnas.sql` lista el esquema real para compararlo con el mapeo. Las bajadas externas (`PASES_SADE`, `GEDO`, `DEVENGADOS`) se mapean sin clave y sólo lectura. Autorizantes, Certificados y Redeterminaciones se vinculan por `CODIGO_AUTORIZANTE`, que no es clave primaria: esa relación se resuelve en los servicios, no como navegación.
- **Páginas:** la URL de cada página se declara una sola vez en `Components/PaginasIvc.cs` y es la misma que se registra en `Auth.Web` para los permisos.
- **Identidad y permisos:** `perms_json` decide a qué páginas entra el usuario (`PermissionPageBase`). Las reglas funcionales de edición que `perms_json` no expresa (rol, área, interruptores de `ABM_PLANI`) viven en `Application/Identidad/ReglasPermisosIvc` como funciones puras sobre un `UsuarioIvc`; `IUsuarioActual` construye ese usuario una vez por circuito desde los claims y la base (áreas por `AREAS.AuthAreaId`, alta y nombre en `UsuariosVinculados`) y `IPermisosIvc` evalúa las reglas con el estado vigente. Los campos inline de las grillas los edita cualquier usuario autenticado; con la planificación cerrada se bloquean sólo alta, ventana y baja (dictamen del área del 04/11/2026). Las reglas se verifican en cada handler que escribe, además de ocultar controles.
- **Auditoría:** toda alta/edición/baja manual queda registrada en `CambiosAuditoria` (quién, cuándo, campo, valor anterior y nuevo).
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

Antes del primer arranque contra una base, ejecutar `Database/001_CambiosAuditoria.sql`. La app redirige al login de `Auth.Web` si el usuario no está autenticado. `/healthz` responde el estado de la conexión a IVC (anónimo).

## 5) Publicación (IIS / Web Deploy)

Igual que SAF: perfil `Properties/PublishProfiles/*.pubxml` fuera de git, con `IsTransformWebConfigDisabled=true` y `SkipExtraFilesOnServer=true`. La configuración real del sitio (connection strings, `SharedCookie__*`, `AuthWeb__BaseUrl`, `AuthWeb__ClientId`) vive como variables de entorno en el `web.config` del servidor y **no debe pisarse** en los deploys.

## 6) CI

`.github/workflows/ci.yml` compila y corre los tests en cada PR y push a `dev` y `main`. Como el proyecto linkea un archivo de `Auth.Web`, el workflow también clona ese repo; requiere el secreto `AUTH_WEB_READ_TOKEN` con permiso de lectura sobre `ATILA2001/Auth.Web`.
