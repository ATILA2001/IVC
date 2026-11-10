#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class ProyectoRepository(IDbContextFactory<IvcDbContext> dbFactory) : IProyectoRepository
{
    private static IQueryable<Proyecto> ConObra(IvcDbContext db) =>
        db.Proyectos.AsNoTracking()
            .Include(p => p.Obra).ThenInclude(o => o.Barrio)
            .Include(p => p.Obra).ThenInclude(o => o.Contrata)
            .Include(p => p.Obra).ThenInclude(o => o.Area)
            .Include(p => p.Obra).ThenInclude(o => o.Empresa)
            .Include(p => p.LineaGestion);

    public async Task<IReadOnlyList<Proyecto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Mismo orden que el legado: por la descripción de la obra.
        return await ConObra(db).OrderBy(p => p.Obra.Descripcion).ThenBy(p => p.Id).ToListAsync(ct);
    }

    public async Task<Proyecto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ConObra(db).FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task AddAsync(Proyecto entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Proyectos.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Proyecto entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Proyectos.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Proyectos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null) return;
        db.Proyectos.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExisteParaObraAsync(int obraId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Proyectos.AsNoTracking().AnyAsync(p => p.ObraId == obraId, ct);
    }
}

public class MovimientoRepository(IDbContextFactory<IvcDbContext> dbFactory) : IMovimientoRepository
{
    private static IQueryable<MovimientoGestion> ConObra(IvcDbContext db) =>
        db.MovimientosGestion.AsNoTracking()
            .Include(m => m.Obra).ThenInclude(o => o.Barrio)
            .Include(m => m.Obra).ThenInclude(o => o.Proyecto).ThenInclude(p => p!.LineaGestion);

    public async Task<IReadOnlyList<MovimientoGestion>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Mismo orden que el legado: por Id (orden de carga).
        return await ConObra(db).OrderBy(m => m.Id).ToListAsync(ct);
    }

    public async Task<MovimientoGestion?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ConObra(db).FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task AddAsync(MovimientoGestion entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.MovimientosGestion.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MovimientoGestion entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.MovimientosGestion.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.MovimientosGestion.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (entity is null) return;
        db.MovimientosGestion.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal?> SumarMontoPorObraAsync(int obraId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.MovimientosGestion.AsNoTracking()
            .Where(m => m.ObraId == obraId)
            .SumAsync(m => m.Monto, ct);
    }
}
