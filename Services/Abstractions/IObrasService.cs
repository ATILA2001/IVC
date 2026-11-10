#nullable enable
using IVC.Application.Common;
using IVC.Application.Obras.Dtos;

namespace IVC.Services.Abstractions;

/// <summary>
/// Obras con sus indicadores. Aplica las reglas del usuario actual: qué obras ve (todas
/// o las de sus áreas), en qué áreas puede crear y si la planificación admite cambios.
/// </summary>
public interface IObrasService
{
    Task<IReadOnlyList<ObraViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<ObraViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ObraViewModel> CreateAsync(ObraViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(ObraViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Áreas en las que el usuario puede dar de alta una obra: todas para el administrador, las propias para el resto.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetAreasPermitidasAsync(CancellationToken ct = default);
}
