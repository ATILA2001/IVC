#nullable enable
using System.Security.Claims;
using IVC.Application.Identidad;

namespace IVC.Services.Abstractions;

/// <summary>
/// Construye el <see cref="UsuarioIvc"/> a partir de los claims de la cookie de Auth.Web y
/// de la base IVC (áreas por AuthAreaId, alta y sincronización en UsuariosVinculados).
/// </summary>
public interface IUsuarioIvcFactory
{
    Task<UsuarioIvc> ConstruirAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    /// <summary>
    /// Clave de la identidad efectiva (persona, nombre, roles, áreas y versión de permisos):
    /// si cambia, el usuario cacheado ya no vale.
    /// </summary>
    string ClaveDeIdentidad(ClaimsPrincipal principal);
}

/// <summary>
/// El usuario actual del circuito. Reemplaza a Session["Usuario"] del legado: se
/// construye una vez y se reutiliza mientras la identidad de la cookie no cambie.
/// Scoped: vive lo que vive el circuito (la pestaña).
/// </summary>
public interface IUsuarioActual
{
    Task<UsuarioIvc> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Descarta el usuario cacheado (por ejemplo tras cambiar su override de planificación).</summary>
    void Invalidar();
}
