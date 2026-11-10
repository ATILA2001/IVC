#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class AreaRepository(IDbContextFactory<IvcDbContext> dbFactory) : IAreaRepository
{
    public async Task<IReadOnlyList<Area>> GetByAuthAreaIdsAsync(IReadOnlyCollection<int> authAreaIds, CancellationToken ct = default)
    {
        if (authAreaIds.Count == 0) return Array.Empty<Area>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Areas.AsNoTracking()
            .Where(a => a.AuthAreaId != null && authAreaIds.Contains(a.AuthAreaId.Value))
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Areas.AsNoTracking().OrderBy(a => a.Nombre).ToListAsync(ct);
    }
}
