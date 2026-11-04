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

Components/                  # UI Blazor (.razor + code-behind .razor.cs)
├─ Account/                  # Login / AccessDenied (delegan en Auth.Web)
├─ Layout/                   # MainLayout: menú armado desde PaginasIvc + perms_json
├─ Pages/                    # Páginas routables, una carpeta por feature
├─ Shared/                   # Toolbar, pie de totales, historial de auditoría, etc.
├─ GridPageBase.cs           # Base de páginas con grilla (carga, edición, auditoría, export)
├─ PermissionPageBase.cs     # Base de páginas con control de permisos
└─ PaginasIvc.cs             # Catálogo de páginas (URL = la registrada en Auth.Web)

Data/                        # EF Core
├─ Entities/                 # Entidades mapeadas a las tablas existentes de IVC
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
- **Base de datos:** el esquema IVC es preexistente y lo comparten el legado, SAF y Power BI. `IvcDbContext` no genera migraciones; las tablas nuevas de la app se crean con scripts numerados en `Database/` (ejecutar primero en `IVC_TEST`).
- **Páginas:** la URL de cada página se declara una sola vez en `Components/PaginasIvc.cs` y es la misma que se registra en `Auth.Web` para los permisos.
- **Auditoría:** toda alta/edición/baja manual queda registrada en `CambiosAuditoria` (quién, cuándo, campo, valor anterior y nuevo).
- **Cultura:** `es-AR` fija (coma decimal, `dd/MM/yyyy`).
- **Inglés** para nombres de capas/carpetas; el dominio funcional va en español.
- **Commits:** Conventional Commits (`feat`, `fix`, `refactor`, `docs`, `chore`). Ramas: `dev` integración, `main` producción, `feature/*`, `fix/*`, `hotfix/*`.

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

Antes del primer arranque contra una base, ejecutar `Database/001_CambiosAuditoria.sql`. La app redirige al login de `Auth.Web` si el usuario no está autenticado. `/healthz` responde el estado de la conexión a IVC (anónimo).

## 5) Publicación (IIS / Web Deploy)

Igual que SAF: perfil `Properties/PublishProfiles/*.pubxml` fuera de git, con `IsTransformWebConfigDisabled=true` y `SkipExtraFilesOnServer=true`. La configuración real del sitio (connection strings, `SharedCookie__*`, `AuthWeb__BaseUrl`, `AuthWeb__ClientId`) vive como variables de entorno en el `web.config` del servidor y **no debe pisarse** en los deploys.

## 6) CI

`.github/workflows/ci.yml` compila y corre los tests en cada PR y push a `dev` y `main`. Como el proyecto linkea un archivo de `Auth.Web`, el workflow también clona ese repo; requiere el secreto `AUTH_WEB_READ_TOKEN` con permiso de lectura sobre `ATILA2001/Auth.Web`.
