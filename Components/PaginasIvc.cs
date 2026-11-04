#nullable enable
namespace IVC.Components;

/// <summary>
/// Catálogo de las páginas de la aplicación: URL (la misma que se registra en Auth.Web
/// y que viaja en perms_json), título, descripción e ícono. Lo consumen el menú lateral
/// y la página de inicio, así las dos listas no se desincronizan. El orden es el del
/// menú. Las páginas se agregan acá a medida que se migran.
/// </summary>
public static class PaginasIvc
{
    public sealed record Pagina(string Url, string Titulo, string Descripcion, string Icono);

    public static readonly IReadOnlyList<Pagina> Todas =
    [
        new("/obras",              "Obras",                  "Obras, montos autorizados, certificados y avance.",            "apartment"),
        new("/autorizantes",       "Autorizantes",           "Autorizantes por obra y seguimiento de redeterminaciones.",     "assignment"),
        new("/certificados",       "Certificados",           "Certificados y reliquidaciones por autorizante.",              "receipt_long"),
        new("/redeterminaciones",  "Redeterminaciones",      "Redeterminaciones de precios, etapas SADE y montos.",          "price_change"),
        new("/legitimos",          "Legítimos abonos",       "Legítimos abonos por obra.",                                   "payments"),
        new("/formulacion",        "Formulación",            "Formulación presupuestaria por obra y año.",                   "table_chart"),
        new("/proyectos",          "Proyectos",              "Base de proyectos y líneas de gestión por obra.",              "folder_open"),
        new("/movimientos",        "Movimientos de gestión", "Movimientos presupuestarios por proyecto.",                    "swap_horiz"),
        new("/barrios",            "Barrios",                "Catálogo de barrios.",                                         "location_city"),
        new("/empresas",           "Empresas",               "Catálogo de empresas contratistas.",                           "business"),
        new("/lineas-gestion",     "Líneas de gestión",      "Catálogo de líneas de gestión.",                               "account_tree"),
        new("/lineas-gestion-ff",  "Líneas de gestión FF",   "Fuentes de financiamiento por línea de gestión.",              "account_balance"),
        new("/admin/configuracion","Configuración",          "Apertura de planificación y formulación; permisos por usuario.","settings"),
    ];
}
