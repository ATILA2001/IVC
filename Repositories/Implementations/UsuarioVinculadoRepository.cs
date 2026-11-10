#nullable enable
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

public class UsuarioVinculadoRepository(IDbContextFactory<IvcDbContext> dbFactory) : IUsuarioVinculadoRepository
{
    /// <summary>Largo de la columna Nombre (nvarchar(50)).</summary>
    public const int NombreMaxLength = 50;

    public async Task<UsuarioVinculado> GetOrCreateAsync(string authUserId, string nombre, CancellationToken ct = default)
    {
        var nombreRecortado = Recortar(string.IsNullOrWhiteSpace(nombre) ? "Usuario" : nombre.Trim());
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fila = await db.UsuariosVinculados.FirstOrDefaultAsync(u => u.AuthUserId == authUserId, ct);
        if (fila is null)
        {
            fila = new UsuarioVinculado { AuthUserId = authUserId, Nombre = nombreRecortado, PlanificacionAbierta = false };
            db.UsuariosVinculados.Add(fila);
            await db.SaveChangesAsync(ct);
        }
        else if (!string.Equals(fila.Nombre, nombreRecortado, StringComparison.Ordinal))
        {
            fila.Nombre = nombreRecortado;
            await db.SaveChangesAsync(ct);
        }
        return fila;
    }

    public async Task<IReadOnlyList<UsuarioVinculado>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.UsuariosVinculados.AsNoTracking().OrderBy(u => u.Nombre).ToListAsync(ct);
    }

    public async Task<bool> SetPlanificacionAbiertaAsync(string authUserId, bool abierta, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fila = await db.UsuariosVinculados.FirstOrDefaultAsync(u => u.AuthUserId == authUserId, ct);
        if (fila is null) return false;
        fila.PlanificacionAbierta = abierta;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string Recortar(string nombre)
        => nombre.Length <= NombreMaxLength ? nombre : nombre[..NombreMaxLength];
}
