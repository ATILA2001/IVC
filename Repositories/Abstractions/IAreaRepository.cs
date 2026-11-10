#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

public interface IAreaRepository
{
    /// <summary>Áreas IVC cuyo AuthAreaId está en la lista (el claim "area" de la cookie), ordenadas por ID.</summary>
    Task<IReadOnlyList<Area>> GetByAuthAreaIdsAsync(IReadOnlyCollection<int> authAreaIds, CancellationToken ct = default);

    Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default);
}
