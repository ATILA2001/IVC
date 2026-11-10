using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Radzen;
using IVC.Data;
using IVC.Repositories.Abstractions;
using IVC.Repositories.Implementations;
using IVC.Security;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;

namespace IVC;

/// <summary>
/// Registro de servicios, un método por concern y en el mismo orden en que los
/// invoca Program.cs. Solo organiza: no cambia qué se registra ni cómo.
/// </summary>
public static class IvcBuilderExtensions
{
    /// <summary>Esquema de la cookie compartida con Auth.Web (el que emite Identity).</summary>
    public const string SharedScheme = "Identity.Application";

    public static WebApplicationBuilder AddPresentation(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddRadzenComponents();
        // Persiste el tema (claro/oscuro) en cookie. IsSecure=false para que la cookie
        // también viaje por http (dev) y el servidor la lea en cada navegación.
        builder.Services.AddRadzenCookieThemeService(options =>
        {
            options.Name = "RadzenTheme";
            options.Duration = TimeSpan.FromDays(365);
            options.IsSecure = false;
        });

        builder.Services.AddCascadingAuthenticationState();

        // Montos y fechas en es-AR en toda la app (coma decimal, dd/MM/yyyy), como el legado.
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture("es-AR")
                   .AddSupportedCultures("es-AR")
                   .AddSupportedUICultures("es-AR");
        });
        return builder;
    }

    public static WebApplicationBuilder AddDatabases(this WebApplicationBuilder builder)
    {
        // Factory, no contexto scoped: en Blazor Server el scope dura todo el circuito del
        // usuario, así que un contexto inyectado se comparte entre operaciones solapadas y
        // EF Core lanza "A second operation was started on this context instance".
        // Cada repositorio crea y descarta el suyo por operación.
        builder.Services.AddDbContextFactory<IvcDbContext>(options =>
            options.UseSqlServer(builder.Configuration.RequireConnectionString("IvcConnection"), sqlOptions =>
                sqlOptions.EnableRetryOnFailure()));

        // Contexto aparte que apunta a la base de Auth.Web para compartir el key ring de
        // DataProtection. Con reintentos: si un corte transitorio impide leer el key ring,
        // ninguna cookie se puede desproteger y todos los usuarios rebotan al login.
        builder.Services.AddDbContext<DataProtectionDbContext>(options =>
            options.UseSqlServer(builder.Configuration.RequireConnectionString("AuthWebConnection"), sqlOptions =>
                sqlOptions.EnableRetryOnFailure()));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // Timeout propio: sin él, la estrategia de reintentos de EF hace que el monitor
        // reciba un cuelgue en vez de un estado cuando la base no responde.
        builder.Services.AddHealthChecks()
            .AddCheck<DbContextHealthCheck<IvcDbContext>>(
                "ivc", failureStatus: null, tags: null, timeout: TimeSpan.FromSeconds(5));

        return builder;
    }

    public static WebApplicationBuilder AddSharedCookieAuth(this WebApplicationBuilder builder)
    {
        var dataProtectionAppName = builder.Configuration["SharedCookie:ApplicationName"];
        if (string.IsNullOrWhiteSpace(dataProtectionAppName))
            throw new InvalidOperationException("SharedCookie:ApplicationName not configured.");

        builder.Services.AddDataProtection()
            .PersistKeysToDbContext<DataProtectionDbContext>()
            .SetApplicationName(dataProtectionAppName);

        var sharedCookieName = builder.Configuration["SharedCookie:Name"];
        if (string.IsNullOrWhiteSpace(sharedCookieName))
            throw new InvalidOperationException("SharedCookie:Name not configured.");
        var sharedCookieDomain = builder.Configuration["SharedCookie:Domain"];

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme          = SharedScheme;
                options.DefaultChallengeScheme = SharedScheme;
            })
            .AddCookie(SharedScheme, options =>
            {
                options.Cookie.Name        = sharedCookieName;
                options.Cookie.Domain      = string.IsNullOrWhiteSpace(sharedCookieDomain)
                                                 ? null : sharedCookieDomain;
                options.Cookie.SameSite    = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.HttpOnly    = true;
                options.SlidingExpiration  = true;
                options.ExpireTimeSpan     = TimeSpan.FromHours(8);
                options.LoginPath          = "/Account/Login";
                options.AccessDeniedPath   = "/Account/AccessDenied";
            });

        builder.Services.AddOptions<CookieAuthenticationOptions>(SharedScheme)
            .PostConfigure<IDataProtectionProvider>((options, dp) =>
            {
                var protector = dp.CreateProtector(
                    "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware",
                    SharedScheme,
                    "v2");
                options.TicketDataFormat = new SharedCookieTicketDataFormat(protector);
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return builder;
    }

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        // Repositorios: Transient, cada uno crea su propio contexto por operación.
        builder.Services.AddTransient<IAreaRepository, AreaRepository>();
        builder.Services.AddTransient<IUsuarioVinculadoRepository, UsuarioVinculadoRepository>();
        builder.Services.AddTransient<IAbmPlaniRepository, AbmPlaniRepository>();
        builder.Services.AddTransient<ICatalogoSimpleRepository, CatalogoSimpleRepository>();
        builder.Services.AddTransient<ILineaGestionRepository, LineaGestionRepository>();
        builder.Services.AddTransient<ILineaGestionFFRepository, LineaGestionFFRepository>();
        builder.Services.AddTransient<IProyectoRepository, ProyectoRepository>();
        builder.Services.AddTransient<IMovimientoRepository, MovimientoRepository>();
        builder.Services.AddTransient<ILookupRepository, LookupRepository>();

        // Catálogos (fase 4). El servicio genérico se cierra con la entidad en cada página
        // (Barrios, Empresas): un registro abierto alcanza para todas.
        builder.Services.AddScoped(typeof(ICatalogoSimpleService<>), typeof(CatalogoSimpleService<>));
        builder.Services.AddScoped<ILineasGestionService, LineasGestionService>();
        builder.Services.AddScoped<ILineasGestionFFService, LineasGestionFFService>();
        builder.Services.AddScoped<IProyectosService, ProyectosService>();
        builder.Services.AddScoped<IMovimientosService, MovimientosService>();
        builder.Services.AddScoped<ILookupService, LookupService>();

        // Identidad IVC: el usuario se construye una vez por circuito (reemplaza a Session["Usuario"]).
        builder.Services.AddScoped<IUsuarioIvcFactory, UsuarioIvcFactory>();
        builder.Services.AddScoped<IUsuarioActual, UsuarioActualService>();
        builder.Services.AddScoped<IPermisosIvc, PermisosIvcService>();
        builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();

        builder.Services.AddScoped<IExportService, ExportService>();
        builder.Services.AddScoped<IVC.Shared.INotificationHelper, IVC.Shared.NotificationHelper>();
        // Scoped a propósito: el estado de las grillas dura lo que dura el circuito.
        builder.Services.AddScoped<IVC.Shared.EstadoGrillas>();
        builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

        // Permisos: lee el claim perms_json de la cookie de Auth.Web.
        builder.Services.AddScoped<IPermissionService, PermissionService>();

        // Revalidación de perms_version contra Auth.Web (fail-open, 5 minutos).
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient("AuthWeb", (sp, client) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var baseUrl = cfg["AuthWeb:BaseUrl"]
                ?? throw new InvalidOperationException("AuthWeb:BaseUrl not configured.");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        builder.Services.AddScoped<IPermissionVersionService, PermissionVersionService>();

        return builder;
    }

    public static WebApplicationBuilder AddHttpsHardening(this WebApplicationBuilder builder)
    {
        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
            options.Preload = false;
        });

        builder.Services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        });

        return builder;
    }

    private static string RequireConnectionString(this IConfiguration cfg, string name)
    {
        var cs = cfg.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException($"Connection string '{name}' not found or empty.");
        return cs;
    }
}
