#nullable enable
using IVC.Application.Obras;
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class ObraRepository(IDbContextFactory<IvcDbContext> dbFactory) : IObraRepository
{
    private static IQueryable<Obra> ConCatalogos(IvcDbContext db) =>
        db.Obras.AsNoTracking()
            .Include(o => o.Empresa)
            .Include(o => o.Area)
            .Include(o => o.Barrio)
            .Include(o => o.Contrata)
            .Include(o => o.Proyecto).ThenInclude(p => p!.LineaGestion);

    public async Task<IReadOnlyList<Obra>> GetAllAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = ConCatalogos(db);
        if (areaIds is not null)
        {
            if (areaIds.Count == 0) return Array.Empty<Obra>();
            query = query.Where(o => areaIds.Contains(o.AreaId));
        }
        return await query.OrderBy(o => o.Descripcion).ThenBy(o => o.Id).ToListAsync(ct);
    }

    public async Task<Obra?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ConCatalogos(db).FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task AddAsync(Obra entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Obras.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Obra entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Obras.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Obras.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (entity is null) return;
        db.Obras.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, AgregadosObra>> GetAgregadosAsync(IReadOnlyCollection<int>? obraIds, CancellationToken ct = default)
    {
        if (obraIds is { Count: 0 }) return new Dictionary<int, AgregadosObra>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Cuatro consultas agrupadas, como el legado (CalculoObraNegocioEF): el filtro por
        // obra se aplica en la base; con obraIds null se agrega todo.
        var autorizantes = db.Autorizantes.AsNoTracking().Where(a => a.ObraId != null);
        var legitimos = db.Legitimos.AsNoTracking().Where(l => l.ObraId != null);
        if (obraIds is not null)
        {
            autorizantes = autorizantes.Where(a => obraIds.Contains(a.ObraId!.Value));
            legitimos = legitimos.Where(l => obraIds.Contains(l.ObraId!.Value));
        }

        var sumasAutorizantes = await autorizantes
            .GroupBy(a => a.ObraId!.Value)
            .Select(g => new
            {
                ObraId = g.Key,
                Suma = g.Sum(a => a.MontoAutorizado) ?? 0m,
                SumaConcepto4 = g.Where(a => a.ConceptoId == IndicadoresObra.ConceptoMontoInicial).Sum(a => a.MontoAutorizado) ?? 0m,
            })
            .ToListAsync(ct);

        var sumasLegitimos = await legitimos
            .GroupBy(l => l.ObraId!.Value)
            .Select(g => new
            {
                ObraId = g.Key,
                Suma = g.Sum(l => l.Certificado) ?? 0m,
                Min = g.Min(l => l.MesAprobacion),
                Max = g.Max(l => l.MesAprobacion),
            })
            .ToListAsync(ct);

        // Certificados vinculados a la obra por CODIGO_AUTORIZANTE (no es clave foránea).
        var certificadosPorObra = await (
            from c in db.Certificados.AsNoTracking()
            join a in autorizantes on c.CodigoAutorizante equals a.CodigoAutorizante
            group c by a.ObraId!.Value into g
            select new
            {
                ObraId = g.Key,
                Suma = g.Sum(c => c.MontoTotal) ?? 0m,
                Min = g.Min(c => c.MesAprobacion),
                Max = g.Max(c => c.MesAprobacion),
            }).ToListAsync(ct);

        // Ejecución física: por autorizante, su monto y la suma de porcentajes de sus certificados.
        var pesos = await autorizantes
            .Select(a => new { ObraId = a.ObraId!.Value, a.CodigoAutorizante, Monto = a.MontoAutorizado ?? 0m })
            .ToListAsync(ct);
        var ejecPorCodigo = await (
            from c in db.Certificados.AsNoTracking()
            join a in autorizantes on c.CodigoAutorizante equals a.CodigoAutorizante
            where c.PorcEjecFisica != null
            group c by c.CodigoAutorizante into g
            select new { Codigo = g.Key, Suma = g.Sum(c => c.PorcEjecFisica) ?? 0m })
            .ToDictionaryAsync(x => x.Codigo, x => x.Suma, ct);

        var autorizantesPorObra = pesos
            .GroupBy(p => p.ObraId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AgregadosObra.AutorizanteEjec>)g
                .Select(p => new AgregadosObra.AutorizanteEjec(
                    p.Monto,
                    p.CodigoAutorizante is not null && ejecPorCodigo.TryGetValue(p.CodigoAutorizante, out var e) ? e : 0m))
                .ToList());

        var ids = sumasAutorizantes.Select(x => x.ObraId)
            .Concat(sumasLegitimos.Select(x => x.ObraId))
            .Concat(certificadosPorObra.Select(x => x.ObraId))
            .Distinct();

        var aut = sumasAutorizantes.ToDictionary(x => x.ObraId);
        var leg = sumasLegitimos.ToDictionary(x => x.ObraId);
        var cer = certificadosPorObra.ToDictionary(x => x.ObraId);

        var resultado = new Dictionary<int, AgregadosObra>();
        foreach (var id in ids)
        {
            aut.TryGetValue(id, out var a);
            leg.TryGetValue(id, out var l);
            cer.TryGetValue(id, out var c);
            resultado[id] = new AgregadosObra(
                SumaAutorizantes: a?.Suma ?? 0m,
                SumaAutorizantesConcepto4: a?.SumaConcepto4 ?? 0m,
                SumaLegitimos: l?.Suma ?? 0m,
                MinLegitimo: l?.Min,
                MaxLegitimo: l?.Max,
                SumaCertificados: c?.Suma ?? 0m,
                MinCertificado: c?.Min,
                MaxCertificado: c?.Max,
                Autorizantes: autorizantesPorObra.TryGetValue(id, out var lista) ? lista : Array.Empty<AgregadosObra.AutorizanteEjec>());
        }
        return resultado;
    }
}
