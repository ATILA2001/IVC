#nullable enable
using IVC.Application.Common;
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class LookupRepository(IDbContextFactory<IvcDbContext> dbFactory) : ILookupRepository
{
    public Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(CancellationToken ct = default) => GetObrasAsync(null, ct);

    public async Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        if (areaIds is { Count: 0 }) return Array.Empty<OpcionLookup>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Obras.AsNoTracking().Include(o => o.Barrio).Include(o => o.Contrata).AsQueryable();
        if (areaIds is not null) query = query.Where(o => areaIds.Contains(o.AreaId));
        var obras = await query.ToListAsync(ct);
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

    public async Task<IReadOnlyList<OpcionLookup>> GetObrasSinFormulacionAsync(IReadOnlyCollection<int> anios, IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        if (anios.Count == 0 || areaIds is { Count: 0 }) return Array.Empty<OpcionLookup>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Obras.AsNoTracking()
            .Include(o => o.Barrio).Include(o => o.Contrata).Include(o => o.Empresa)
            .Where(o => !db.Formulaciones.Any(f => f.ObraId == o.Id && anios.Contains(f.FechaPeriodo.Year)));
        if (areaIds is not null) query = query.Where(o => areaIds.Contains(o.AreaId));
        var obras = await query.ToListAsync(ct);
        return obras.Select(o => new OpcionLookup(o.Id, TextoObra(o, conEmpresa: true)))
            .OrderBy(x => x.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetUnidadesMedidaAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UnidadesMedida.AsNoTracking().OrderBy(u => u.Nombre)
            .Select(u => new OpcionLookup(u.Id, u.Nombre ?? string.Empty)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetPrioridadesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Prioridades.AsNoTracking().OrderBy(p => p.Nombre)
            .Select(p => new OpcionLookup(p.Id, p.Nombre ?? string.Empty)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetLineasGestionAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.LineasGestion.AsNoTracking()
            .OrderBy(l => l.Nombre)
            .Select(l => new OpcionLookup(l.Id, l.Nombre ?? string.Empty))
            .ToListAsync(ct);
    }

    // El OrderBy va ANTES del Select: ordenar por una propiedad del record proyectado no
    // se traduce a SQL (la base en memoria de los tests lo acepta; SQL Server no).
    public Task<IReadOnlyList<OpcionLookup>> GetAreasAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.Areas.OrderBy(a => a.Nombre).Select(a => new OpcionLookup(a.Id, a.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetEmpresasAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.Empresas.OrderBy(e => e.Nombre).Select(e => new OpcionLookup(e.Id, e.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetContratasAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.Contratas.OrderBy(c => c.Nombre).Select(c => new OpcionLookup(c.Id, c.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetBarriosAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.Barrios.OrderBy(b => b.Nombre).Select(b => new OpcionLookup(b.Id, b.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetConceptosAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.Conceptos.OrderBy(c => c.Nombre).Select(c => new OpcionLookup(c.Id, c.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetEstadosAutorizanteAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.EstadosAutorizante.OrderBy(e => e.Nombre).Select(e => new OpcionLookup(e.Id, e.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetEstadosRedetAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.EstadosRedet.OrderBy(e => e.Nombre).Select(e => new OpcionLookup(e.Id, e.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetTiposPagoAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.TiposPago.OrderBy(t => t.Nombre).Select(t => new OpcionLookup(t.Id, t.Nombre ?? string.Empty)), ct);

    public Task<IReadOnlyList<OpcionLookup>> GetUsuariosRedeterminacionesAsync(CancellationToken ct = default) =>
        CatalogoAsync(db => db.Usuarios
            .Where(u => u.Estado == true && u.AreaId == IVC.Application.Identidad.IvcAreaIds.Redeterminaciones)
            .OrderBy(u => u.Nombre)
            .Select(u => new OpcionLookup(u.Id, u.Nombre ?? string.Empty)), ct);

    public async Task<IReadOnlyList<OpcionLookup>> GetAutorizantesAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.Autorizantes.AsNoTracking().Include(a => a.Obra!).ThenInclude(o => o.Empresa).AsQueryable();
        if (areaIds is not null)
        {
            if (areaIds.Count == 0) return Array.Empty<OpcionLookup>();
            query = query.Where(a => a.Obra != null && areaIds.Contains(a.Obra.AreaId));
        }
        var autorizantes = await query.OrderBy(a => a.CodigoAutorizante).ToListAsync(ct);
        // Texto del desplegable del legado: código - obra - empresa - detalle.
        return autorizantes.Select(a => new OpcionLookup(a.Id, string.Join(" - ",
                new[] { a.CodigoAutorizante, a.Obra?.Descripcion, a.Obra?.Empresa?.Nombre, a.Detalle }
                    .Where(p => !string.IsNullOrWhiteSpace(p)))))
            .ToList();
    }

    private async Task<IReadOnlyList<OpcionLookup>> CatalogoAsync(Func<IvcDbContext, IQueryable<OpcionLookup>> consulta, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await consulta(db).AsNoTracking().ToListAsync(ct);
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
