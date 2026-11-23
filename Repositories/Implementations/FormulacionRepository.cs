#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class FormulacionRepository(IDbContextFactory<IvcDbContext> dbFactory) : IFormulacionRepository
{
    private static IQueryable<Formulacion> Completo(IvcDbContext db) =>
        db.Formulaciones.AsNoTracking()
            .Include(f => f.Obra).ThenInclude(o => o.Area)
            .Include(f => f.Obra).ThenInclude(o => o.Empresa)
            .Include(f => f.Obra).ThenInclude(o => o.Contrata)
            .Include(f => f.Obra).ThenInclude(o => o.Barrio)
            .Include(f => f.Obra).ThenInclude(o => o.Proyecto).ThenInclude(p => p!.LineaGestion)
            .Include(f => f.UnidadMedida)
            .Include(f => f.Prioridad);

    public async Task<IReadOnlyList<Formulacion>> GetDelCicloAsync(IReadOnlyCollection<int> anios, IReadOnlyCollection<int>? areaIds, CancellationToken ct = default)
    {
        if (anios.Count == 0 || areaIds is { Count: 0 }) return Array.Empty<Formulacion>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = Completo(db).Where(f => anios.Contains(f.FechaPeriodo.Year));
        if (areaIds is not null)
            query = query.Where(f => areaIds.Contains(f.Obra.AreaId));
        // Mismo orden que el legado: por la descripción de la obra; dentro de la obra, por año.
        return await query.OrderBy(f => f.Obra.Descripcion).ThenBy(f => f.ObraId).ThenBy(f => f.FechaPeriodo).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Formulacion>> GetDeObraAsync(int obraId, IReadOnlyCollection<int> anios, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Completo(db)
            .Where(f => f.ObraId == obraId && anios.Contains(f.FechaPeriodo.Year))
            .OrderBy(f => f.FechaPeriodo)
            .ToListAsync(ct);
    }

    public async Task GuardarPivotAsync(int obraId, IReadOnlyList<int> anios, Formulacion compartido, IReadOnlyList<decimal?> montos, CancellationToken ct = default)
    {
        if (montos.Count != anios.Count)
            throw new ArgumentException("Debe venir un monto (o null) por cada año del ciclo.", nameof(montos));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existentes = await db.Formulaciones
            .Where(f => f.ObraId == obraId && anios.Contains(f.FechaPeriodo.Year))
            .ToListAsync(ct);

        for (var i = 0; i < anios.Count; i++)
        {
            var anio = anios[i];
            var fila = existentes.FirstOrDefault(f => f.FechaPeriodo.Year == anio);

            if (montos[i] is not decimal monto)
            {
                if (fila is not null) db.Formulaciones.Remove(fila);
                continue;
            }

            if (fila is null)
            {
                fila = new Formulacion { ObraId = obraId, FechaPeriodo = new DateTime(anio, 1, 1) };
                db.Formulaciones.Add(fila);
            }

            fila.Monto = monto;
            fila.MesBase = compartido.MesBase;
            fila.Observaciones = compartido.Observaciones;
            fila.Ppi = compartido.Ppi;
            fila.Techos = compartido.Techos;
            fila.UnidadMedidaId = compartido.UnidadMedidaId;
            fila.ValorMedida = compartido.ValorMedida;
            fila.PrioridadId = compartido.PrioridadId;
            fila.BreveDescripcion = compartido.BreveDescripcion;
            fila.FechaInicio = compartido.FechaInicio;
            fila.FechaFin = compartido.FechaFin;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> EliminarDeObraAsync(int obraId, IReadOnlyCollection<int> anios, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var filas = await db.Formulaciones
            .Where(f => f.ObraId == obraId && anios.Contains(f.FechaPeriodo.Year))
            .ToListAsync(ct);
        if (filas.Count == 0) return false;
        db.Formulaciones.RemoveRange(filas);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
