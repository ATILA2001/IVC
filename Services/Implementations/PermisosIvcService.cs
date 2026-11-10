#nullable enable
using IVC.Application.Identidad;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;

namespace IVC.Services.Implementations;

public sealed class PermisosIvcService(IUsuarioActual usuarioActual, IAbmPlaniRepository abmPlani) : IPermisosIvc
{
    public Task<UsuarioIvc> UsuarioAsync(CancellationToken ct = default) => usuarioActual.ObtenerAsync(ct);

    // El estado se lee en cada consulta, como el legado: un administrador puede cerrar la
    // planificación mientras otro usuario tiene la grilla abierta, y la escritura siguiente
    // tiene que rechazarse.
    public Task<EstadoPlanificacion> EstadoPlanificacionAsync(CancellationToken ct = default) => abmPlani.GetAsync(ct);

    public async Task<bool> PuedeModificarPlanificacionAsync(CancellationToken ct = default)
        => ReglasPermisosIvc.PuedeModificarPlanificacion(await UsuarioAsync(ct), await EstadoPlanificacionAsync(ct));

    public async Task<bool> PuedeEditarInlineAsync(CancellationToken ct = default)
        => ReglasPermisosIvc.PuedeEditarInline(await UsuarioAsync(ct));

    public async Task<bool> PuedeModificarFormulacionAsync(CancellationToken ct = default)
        => ReglasPermisosIvc.PuedeModificarFormulacion(await UsuarioAsync(ct), await EstadoPlanificacionAsync(ct));

    public async Task<bool> PuedeModificarRedeterminacionesAsync(CancellationToken ct = default)
        => ReglasPermisosIvc.PuedeModificarRedeterminaciones(await UsuarioAsync(ct));
}
