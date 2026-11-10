#nullable enable
using IVC.Application.Identidad;
using IVC.Application.Identidad.Dtos;

namespace IVC.Services.Abstractions;

/// <summary>
/// Panel de configuración (sólo administradores): interruptores globales de planificación
/// y formulación, y override de planificación por usuario. Cada cambio queda auditado.
/// </summary>
public interface IConfiguracionService
{
    Task<EstadoPlanificacion> ObtenerEstadoAsync(CancellationToken ct = default);
    Task SetPlanificacionAbiertaAsync(bool abierta, string usuario, CancellationToken ct = default);
    Task SetFormulacionAbiertaAsync(bool abierta, string usuario, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioVinculadoViewModel>> ListarUsuariosAsync(CancellationToken ct = default);
    Task SetOverridePlanificacionAsync(string authUserId, bool abierta, string usuario, CancellationToken ct = default);
}
