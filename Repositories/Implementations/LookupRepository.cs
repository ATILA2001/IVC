#nullable enable
using IVC.Application.Common;
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class LookupRepository(IDbContextFactory<IvcDbContext> dbFactory) : ILookupRepository
{
    public async Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var obras = await db.Obras.AsNoTracking()
            .Include(o => o.Barrio).Include(o => o.Contrata)
            .ToListAsync(ct);
        return obras.Select(o => new OpcionLookup(o.Id, TextoObra(o, conEmpresa: false)))
            .OrderBy(x => x.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetObrasSinProyectoAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var obras = await db.Obras.AsNoTracking()
            .Include(o => o.Barrio).Include(o => o.Contrata).Include(o => o.Empresa)
            .Where(o => !db.Proyectos.Any(p => p.ObraId == o.Id))
            .ToListAsync(ct);
        return obras.Select(o => new OpcionLookup(o.Id, TextoObra(o, conEmpresa: true)))
            .OrderBy(x => x.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetLineasGestionAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestion.AsNoTracking()
            .OrderBy(l => l.Nombre)
            .Select(l => new OpcionLookup(l.Id, l.Nombre ?? string.Empty))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Texto del desplegable de obras del legado: barrio - descripción [- empresa] -
    /// contrata - número/año. Las partes sin dato se omiten (el legado las excluía con
    /// INNER JOIN; acá la obra sigue apareciendo).
    /// </summary>
    public static string TextoObra(Obra o, bool conEmpresa)
    {
        var partes = new List<string?> { o.Barrio?.Nombre, o.Descripcion };
        if (conEmpresa) partes.Add(o.Empresa?.Nombre);
        partes.Add(o.Contrata?.Nombre);
        if (o.Numero is not null || o.Anio is not null) partes.Add($"{o.Numero}/{o.Anio}");
        return string.Join(" - ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
