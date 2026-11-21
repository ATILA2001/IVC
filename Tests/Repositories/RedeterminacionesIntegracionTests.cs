using IVC.Application.Redeterminaciones;
using IVC.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// Lo que las reglas de redeterminaciones dan por sentado sobre ESTADOS_REDET en una base IVC
/// real (sólo lectura): que los ids que usan R6 y el mapeo a estado general existen con los
/// nombres esperados, y que las etapas de la tabla de correspondencia etapa-buzón siguen
/// existiendo con el mismo nombre (una renombrada dejaría de detectar discrepancias en silencio).
/// Corre sólo con IVC_TEST_CONNECTION.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class RedeterminacionesIntegracionTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    private static IvcDbContext? Crear()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) return null;
        return new IvcDbContext(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options);
    }

    [TestMethod]
    public async Task EtapasDeLasReglas_ExistenEnEstadosRedet()
    {
        await using var db = Crear();
        if (db is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var etapas = await db.EstadosRedet.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Nombre ?? string.Empty);
        var usadas = ReglasRedeterminaciones.EtapasDesestimadas
            .Concat(MapeoEstadoRedet.EtapasNoIniciadas)
            .Concat(MapeoEstadoRedet.EtapasAprobadas)
            .Distinct().OrderBy(id => id).ToList();

        var faltan = usadas.Where(id => !etapas.ContainsKey(id)).ToList();
        Assert.AreEqual(0, faltan.Count, $"Etapas usadas por las reglas que no existen en ESTADOS_REDET: {string.Join(", ", faltan)}");
        foreach (var id in usadas)
            Console.WriteLine($"{id} = {etapas[id]}");
        StringAssert.Contains(etapas[35].ToUpperInvariant(), "RECHAZ");
        StringAssert.Contains(etapas[36].ToUpperInvariant(), "PLAZO");
    }

    [TestMethod]
    public async Task EtapasDelMatcherDeBuzones_CoincidenConLosNombresDeEstadosRedet()
    {
        await using var db = Crear();
        if (db is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var nombres = (await db.EstadosRedet.AsNoTracking().Select(e => e.Nombre).ToListAsync())
            .Where(n => n is not null).Select(n => n!.Trim()).ToHashSet(StringComparer.Ordinal);
        var sinCorrespondencia = BuzonEtapaMatcher.EtapasConocidas.Where(e => !nombres.Contains(e)).ToList();

        Console.WriteLine($"ESTADOS_REDET: {nombres.Count} etapas; matcher: {BuzonEtapaMatcher.EtapasConocidas.Count()}; sin coincidencia exacta: {sinCorrespondencia.Count}");
        foreach (var e in sinCorrespondencia) Console.WriteLine($"  sin coincidencia: \"{e}\"");
        // La etapa se compara por nombre exacto: una que no coincida nunca marca discrepancia
        // (el legado tenía dos así, por un guion faltante). Verificado contra IVC_TEST el 10/11/2026.
        Assert.AreEqual(0, sinCorrespondencia.Count, "etapas del matcher que no existen con ese nombre en ESTADOS_REDET");
    }
}
