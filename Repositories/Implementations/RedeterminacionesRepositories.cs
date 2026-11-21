#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class AutorizanteRepository(IDbContextFactory<IvcDbContext> dbFactory) : IAutorizanteRepository
{
    private static IQueryable<Autorizante> Completo(IvcDbContext db) =>
        db.Autorizantes.AsNoTracking()
            .Include(a => a.Obra!).ThenInclude(o => o.Area)
            .Include(a => a.Obra!).ThenInclude(o => o.Barrio)
            .Include(a => a.Obra!).ThenInclude(o => o.Empresa)
            .Include(a => a.Obra!).ThenInclude(o => o.Contrata)
            .Include(a => a.Obra!).ThenInclude(o => o.Proyecto).ThenInclude(p => p!.LineaGestion)
            .Include(a => a.Estado)
            .Include(a => a.Concepto);

    public async Task<IReadOnlyList<Autorizante>> GetAllAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = Completo(db);
        if (areaIds is not null)
        {
            if (areaIds.Count == 0) return Array.Empty<Autorizante>();
            query = query.Where(a => a.Obra != null && areaIds.Contains(a.Obra.AreaId));
        }
        return await query.OrderBy(a => a.CodigoAutorizante).ThenBy(a => a.Id).ToListAsync(ct);
    }

    public async Task<Autorizante?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Completo(db).FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<Autorizante>> GetPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct = default)
    {
        if (codigos.Count == 0) return Array.Empty<Autorizante>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Completo(db).Where(a => a.CodigoAutorizante != null && codigos.Contains(a.CodigoAutorizante)).ToListAsync(ct);
    }

    public async Task AddAsync(Autorizante entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Autorizantes.Add(entity);
        await db.SaveChangesAsync(ct);
        // El código lo arma el trigger (OBRA-n): se relee para devolverlo a la fila en pantalla.
        await db.Entry(entity).ReloadAsync(ct);
    }

    public async Task UpdateAsync(Autorizante entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Autorizantes.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Autorizantes.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return;
        db.Autorizantes.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TieneDependenciasAsync(string codigoAutorizante, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Certificados.AsNoTracking().AnyAsync(c => c.CodigoAutorizante == codigoAutorizante, ct)
            || await db.Redeterminaciones.AsNoTracking().AnyAsync(r => r.CodigoAutorizante == codigoAutorizante, ct);
    }

    public async Task<decimal> SumaCertificadosAsync(string codigoAutorizante, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Certificados.AsNoTracking()
            .Where(c => c.CodigoAutorizante == codigoAutorizante)
            .SumAsync(c => c.MontoTotal, ct) ?? 0m;
    }

    public async Task<bool> UpdateSeguimientoAsync(int id, string? expediente, int? estadoId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Autorizantes.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return false;
        entity.Expediente = expediente;
        if (estadoId is not null) entity.EstadoId = estadoId;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public class CertificadoRepository(IDbContextFactory<IvcDbContext> dbFactory) : ICertificadoRepository
{
    public async Task<IReadOnlyList<Certificado>> GetPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct = default)
    {
        if (codigos.Count == 0) return Array.Empty<Certificado>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Certificados.AsNoTracking()
            .Include(c => c.TipoPago)
            .Where(c => codigos.Contains(c.CodigoAutorizante))
            .OrderBy(c => c.CodigoAutorizante).ThenBy(c => c.MesAprobacion).ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<Certificado?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Certificados.AsNoTracking().Include(c => c.TipoPago).FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task AddAsync(Certificado entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Certificados.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Certificado entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Certificados.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Certificados.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return;
        db.Certificados.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetEjecFisicaPorCodigoAsync(IReadOnlyCollection<string> codigos, CancellationToken ct = default)
    {
        if (codigos.Count == 0) return new Dictionary<string, decimal>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Certificados.AsNoTracking()
            .Where(c => codigos.Contains(c.CodigoAutorizante) && c.PorcEjecFisica != null)
            .GroupBy(c => c.CodigoAutorizante)
            .Select(g => new { Codigo = g.Key, Suma = g.Sum(c => c.PorcEjecFisica) ?? 0m })
            .ToDictionaryAsync(x => x.Codigo, x => x.Suma, ct);
    }
}

public class LegitimoRepository(IDbContextFactory<IvcDbContext> dbFactory) : ILegitimoRepository
{
    private static IQueryable<Legitimo> Completo(IvcDbContext db) =>
        db.Legitimos.AsNoTracking()
            .Include(l => l.Obra!).ThenInclude(o => o.Area)
            .Include(l => l.Obra!).ThenInclude(o => o.Barrio)
            .Include(l => l.Obra!).ThenInclude(o => o.Empresa)
            .Include(l => l.Obra!).ThenInclude(o => o.Proyecto).ThenInclude(p => p!.LineaGestion);

    public async Task<IReadOnlyList<Legitimo>> GetAllAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = Completo(db);
        if (areaIds is not null)
        {
            if (areaIds.Count == 0) return Array.Empty<Legitimo>();
            query = query.Where(l => l.Obra != null && areaIds.Contains(l.Obra.AreaId));
        }
        // Mismo orden que el legado: por la descripción de la obra.
        return await query.OrderBy(l => l.Obra!.Descripcion).ThenBy(l => l.Id).ToListAsync(ct);
    }

    public async Task<Legitimo?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Completo(db).FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task AddAsync(Legitimo entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Legitimos.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Legitimo entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Legitimos.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Legitimos.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (entity is null) return;
        db.Legitimos.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

public class RedeterminacionRepository(IDbContextFactory<IvcDbContext> dbFactory) : IRedeterminacionRepository
{
    public async Task<IReadOnlyList<Redeterminacion>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Mismo orden que el legado: las más nuevas primero.
        return await db.Redeterminaciones.AsNoTracking()
            .Include(r => r.Etapa).Include(r => r.Usuario)
            .OrderByDescending(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Redeterminacion>> GetPorCodigosAsync(IReadOnlyCollection<string> codigosAutorizante, CancellationToken ct = default)
    {
        if (codigosAutorizante.Count == 0) return Array.Empty<Redeterminacion>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Redeterminaciones.AsNoTracking()
            .Include(r => r.Etapa)
            .Where(r => r.CodigoAutorizante != null && codigosAutorizante.Contains(r.CodigoAutorizante))
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<Redeterminacion?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Redeterminaciones.AsNoTracking()
            .Include(r => r.Etapa).Include(r => r.Usuario)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task AddAsync(Redeterminacion entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Redeterminaciones.Add(entity);
        await db.SaveChangesAsync(ct);
        // CODIGO_REDET es columna calculada: se relee.
        await db.Entry(entity).ReloadAsync(ct);
    }

    public async Task UpdateAsync(Redeterminacion entity, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.Redeterminaciones.Update(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Redeterminaciones.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return;
        db.Redeterminaciones.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateSeguimientoAsync(int id, int? etapaId, string? expediente, int? usuarioId, bool borrarUsuario, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Redeterminaciones.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return false;
        if (etapaId is not null) entity.EtapaId = etapaId;
        if (expediente is not null) entity.Expediente = expediente;
        if (borrarUsuario) entity.UsuarioId = null;
        else if (usuarioId is not null) entity.UsuarioId = usuarioId;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public class ExpedienteReliqRepository(IDbContextFactory<IvcDbContext> dbFactory) : IExpedienteReliqRepository
{
    public async Task<IReadOnlyList<ExpedienteReliq>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ExpedientesReliq.AsNoTracking().ToListAsync(ct);
    }

    public async Task GuardarOActualizarAsync(string codigoRedet, DateTime mesAprobacion, string? expediente, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Se compara por año y mes, como la lógica original.
        var existente = await db.ExpedientesReliq.FirstOrDefaultAsync(e =>
            e.CodigoRedet == codigoRedet && e.MesAprobacion.Year == mesAprobacion.Year && e.MesAprobacion.Month == mesAprobacion.Month, ct);
        if (existente is not null)
            existente.Expediente = expediente ?? string.Empty;
        else
            db.ExpedientesReliq.Add(new ExpedienteReliq { CodigoRedet = codigoRedet, MesAprobacion = mesAprobacion.Date, Expediente = expediente ?? string.Empty });
        await db.SaveChangesAsync(ct);
    }
}
