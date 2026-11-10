#nullable enable
using IVC.Application.Identidad;

namespace IVC.Services.Abstractions;

/// <summary>
/// Reglas funcionales de edición evaluadas para el usuario actual con el estado vigente
/// de ABM_PLANI. Las páginas las consultan al cargar y vuelven a consultarlas en cada
/// handler que escribe: la UI oculta, el servidor decide.
/// </summary>
public interface IPermisosIvc
{
    Task<UsuarioIvc> UsuarioAsync(CancellationToken ct = default);
    Task<EstadoPlanificacion> EstadoPlanificacionAsync(CancellationToken ct = default);
    Task<bool> PuedeModificarPlanificacionAsync(CancellationToken ct = default);
    Task<bool> PuedeEditarInlineAsync(CancellationToken ct = default);
    Task<bool> PuedeModificarFormulacionAsync(CancellationToken ct = default);
    Task<bool> PuedeModificarRedeterminacionesAsync(CancellationToken ct = default);
}
