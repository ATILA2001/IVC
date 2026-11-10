#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class LineaGestionRepository(IDbContextFactory<IvcDbContext> dbFactory) : ILineaGestionRepository
{
    public async Task<IReadOnlyList<LineaGestion>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestion.AsNoTracking().OrderBy(x => x.Nombre).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<LineaGestion?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestion.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task AddAsync(LineaGestion entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.LineasGestion.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LineaGestion entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.LineasGestion.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.LineasGestion.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return;
        db.LineasGestion.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExisteNombreAsync(string nombre, int idExcluido, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestion.AsNoTracking().AnyAsync(x => x.Nombre == nombre && x.Id != idExcluido, ct);
    }
}

public class LineaGestionFFRepository(IDbContextFactory<IvcDbContext> dbFactory) : ILineaGestionFFRepository
{
    public async Task<IReadOnlyList<LineaGestionFF>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestionFF.AsNoTracking()
            .Include(x => x.LineaGestion)
            .OrderBy(x => x.Nombre).ThenBy(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<LineaGestionFF?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestionFF.AsNoTracking()
            .Include(x => x.LineaGestion)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task AddAsync(LineaGestionFF entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.LineasGestionFF.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LineaGestionFF entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.LineasGestionFF.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.LineasGestionFF.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return;
        db.LineasGestionFF.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
