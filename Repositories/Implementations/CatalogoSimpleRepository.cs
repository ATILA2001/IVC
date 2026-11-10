#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class CatalogoSimpleRepository(IDbContextFactory<IvcDbContext> dbFactory) : ICatalogoSimpleRepository
{
    public async Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken ct = default) where T : class, ICatalogoSimple
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // EF.Property y no x.Nombre: el acceso por miembro de interfaz no siempre se
        // traduce a SQL; por nombre de propiedad la traducción es directa.
        return await db.Set<T>().AsNoTracking()
            .OrderBy(x => EF.Property<string?>(x, nameof(ICatalogoSimple.Nombre)))
            .ThenBy(x => EF.Property<int>(x, nameof(ICatalogoSimple.Id)))
            .ToListAsync(ct);
    }

    public async Task<T?> GetByIdAsync<T>(int id, CancellationToken ct = default) where T : class, ICatalogoSimple
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Set<T>().AsNoTracking()
            .FirstOrDefaultAsync(x => EF.Property<int>(x, nameof(ICatalogoSimple.Id)) == id, ct);
    }

    public async Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class, ICatalogoSimple
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class, ICatalogoSimple
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Set<T>().Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync<T>(int id, CancellationToken ct = default) where T : class, ICatalogoSimple
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Set<T>()
            .FirstOrDefaultAsync(x => EF.Property<int>(x, nameof(ICatalogoSimple.Id)) == id, ct);
        if (entity is null) return; // ya no existe: es el resultado buscado
        db.Set<T>().Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExisteNombreAsync<T>(string nombre, int idExcluido, CancellationToken ct = default) where T : class, ICatalogoSimple
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // La comparación corre en SQL Server con la collation de la base (sin distinguir
        // mayúsculas), que es el criterio real de unicidad.
        return await db.Set<T>().AsNoTracking()
            .AnyAsync(x => EF.Property<string?>(x, nameof(ICatalogoSimple.Nombre)) == nombre
                        && EF.Property<int>(x, nameof(ICatalogoSimple.Id)) != idExcluido, ct);
    }
}
