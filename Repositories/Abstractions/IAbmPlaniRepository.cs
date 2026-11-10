#nullable enable
using IVC.Application.Identidad;

namespace IVC.Repositories.Abstractions;

/// <summary>Interruptores globales de planificación y formulación (tabla ABM_PLANI, una fila).</summary>
public interface IAbmPlaniRepository
{
    /// <summary>Estado actual; si la tabla está vacía, todo cerrado.</summary>
    Task<EstadoPlanificacion> GetAsync(CancellationToken ct = default);

    /// <summary>Persiste el estado en la única fila (la crea si no existe).</summary>
    Task SetAsync(EstadoPlanificacion estado, CancellationToken ct = default);
}
