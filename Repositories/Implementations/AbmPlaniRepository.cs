#nullable enable
using IVC.Application.Identidad;
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class AbmPlaniRepository(IDbContextFactory<IvcDbContext> dbFactory) : IAbmPlaniRepository
{
    public async Task<EstadoPlanificacion> GetAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Como el legado: la primera fila es la configuración; sin filas, todo cerrado.
        var fila = await db.AbmPlani.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
        return fila is null
            ? EstadoPlanificacion.Cerrado
            : new EstadoPlanificacion(fila.PlanificacionAbierta, fila.FormulacionAbierta);
    }

    public async Task SetAsync(EstadoPlanificacion estado, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fila = await db.AbmPlani.OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
        if (fila is null)
        {
            fila = new AbmPlani();
            db.AbmPlani.Add(fila);
        }
        fila.PlanificacionAbierta = estado.PlanificacionAbierta;
        fila.FormulacionAbierta = estado.FormulacionAbierta;
        await db.SaveChangesAsync(ct);
    }
}
