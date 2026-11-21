#nullable enable
using IVC.Application.Redeterminaciones;
using IVC.Data;
using IVC.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Repositories.Implementations;

/// <summary>
/// Portado de DatosExternosNegocioEF del legado, sin la caché de sesión: en Blazor Server
/// Los diccionarios se indexan por expediente recortado y sin distinguir mayúsculas, que es
/// como compara SQL Server (y la vista vw_PBI_Pagos): un expediente con espacios al final
/// en CERTIFICADOS encontraba el devengado en SQL pero no en el diccionario.
/// la grilla conserva sus datos mientras está abierta y se consulta una vez por carga.
/// Las listas de expedientes se consultan en lotes para no depender del límite de
/// parámetros de SQL Server.
/// </summary>
public class DatosExternosRepository(IDbContextFactory<IvcDbContext> dbFactory) : IDatosExternosRepository
{
    /// <summary>Tamaño de lote de las consultas IN (...): el legado usaba 800 por el límite de parámetros.</summary>
    public const int TamanioLote = 800;

    public async Task<IReadOnlyDictionary<string, UltimoPaseSade>> GetUltimosPasesSadeAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default)
    {
        var resultado = new Dictionary<string, UltimoPaseSade>(StringComparer.OrdinalIgnoreCase);
        var validos = Limpiar(expedientes);
        if (validos.Count == 0) return resultado;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        foreach (var lote in Lotes(validos))
        {
            // "TOP 1 ... ORDER BY fecha DESC" por expediente, resuelto en memoria sobre las
            // filas del lote (la tabla tiene en la práctica una fila por expediente).
            var pases = await db.PasesSade.AsNoTracking()
                .Where(p => p.Expediente != null && lote.Contains(p.Expediente))
                .Select(p => new { p.Expediente, p.BuzonDestino, p.FechaUltimoPase })
                .ToListAsync(ct);
            foreach (var grupo in pases.GroupBy(p => p.Expediente!.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var ultimo = grupo.OrderByDescending(p => p.FechaUltimoPase).First();
                resultado[grupo.Key] = new UltimoPaseSade(ultimo.BuzonDestino, ultimo.FechaUltimoPase);
            }
        }
        return resultado;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetDevengadosAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default)
    {
        var resultado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var validos = Limpiar(expedientes);
        if (validos.Count == 0) return resultado;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        foreach (var lote in Lotes(validos))
        {
            var sumas = await db.Devengados.AsNoTracking()
                .Where(d => d.EeFinanciera != null && lote.Contains(d.EeFinanciera))
                .GroupBy(d => d.EeFinanciera!)
                .Select(g => new { Expediente = g.Key, Total = g.Sum(d => d.ImportePp) ?? 0m })
                .ToListAsync(ct);
            foreach (var s in sumas)
                resultado[s.Expediente.Trim()] = resultado.GetValueOrDefault(s.Expediente.Trim()) + s.Total;
        }
        return resultado;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAcdirsAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default)
    {
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var validos = Limpiar(expedientes);
        if (validos.Count == 0) return resultado;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        foreach (var lote in Lotes(validos))
        {
            // La fila de GEDO con la mayor FECHA_FIRMA por expediente.
            var filas = await db.Gedos.AsNoTracking()
                .Where(g => g.Expediente != null && lote.Contains(g.Expediente))
                .Select(g => new { g.Expediente, g.Acdir, g.FechaFirma })
                .ToListAsync(ct);
            foreach (var grupo in filas.GroupBy(f => f.Expediente!.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var ultima = grupo.OrderByDescending(f => f.FechaFirma).First();
                if (!string.IsNullOrEmpty(ultima.Acdir))
                    resultado[grupo.Key] = ultima.Acdir;
            }
        }
        return resultado;
    }

    private static List<string> Limpiar(IReadOnlyCollection<string> expedientes) =>
        expedientes.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private static IEnumerable<List<string>> Lotes(List<string> valores)
    {
        for (var i = 0; i < valores.Count; i += TamanioLote)
            yield return valores.GetRange(i, Math.Min(TamanioLote, valores.Count - i));
    }
}
