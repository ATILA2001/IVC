using IVC.Data;
using IVC.Repositories.Implementations;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// Los repositorios de catálogos contra una base IVC real (sólo lectura): que las
/// consultas con Include y subconsultas se traduzcan a SQL, y que el "Autorizado nuevo"
/// de Movimientos coincida fila por fila con la consulta del legado. Corre sólo con
/// IVC_TEST_CONNECTION (apuntar a IVC_TEST, nunca a producción); sin ella queda Inconclusive.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class CatalogosIntegracionTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private static Factory? Crear()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) return null;
        return new Factory(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options);
    }

    [TestMethod]
    public async Task LosCatalogosYLosLookups_SeLeen()
    {
        var factory = Crear();
        if (factory is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var simples = new CatalogoSimpleRepository(factory);
        var barrios = await simples.GetAllAsync<IVC.Data.Entities.Barrio>();
        var empresas = await simples.GetAllAsync<IVC.Data.Entities.Empresa>();
        var lineas = await new LineaGestionRepository(factory).GetAllAsync();
        var ff = await new LineaGestionFFRepository(factory).GetAllAsync();
        var lookup = new LookupRepository(factory);
        var obras = await lookup.GetObrasAsync();
        var sinProyecto = await lookup.GetObrasSinProyectoAsync();
        // Los desplegables de Obras: una proyección mal ordenada no se traduce a SQL y
        // sólo se nota contra la base real.
        var areas = await lookup.GetAreasAsync();
        var empresasLookup = await lookup.GetEmpresasAsync();
        var contratas = await lookup.GetContratasAsync();
        var barriosLookup = await lookup.GetBarriosAsync();
        Assert.IsTrue(areas.Count > 0 && empresasLookup.Count > 0 && contratas.Count > 0 && barriosLookup.Count > 0);
        CollectionAssert.AreEqual(areas.Select(a => a.Nombre).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList(), areas.Select(a => a.Nombre).ToList(), "ordenadas por nombre");

        Console.WriteLine($"Barrios {barrios.Count}, empresas {empresas.Count}, líneas {lineas.Count}, FF {ff.Count}, obras {obras.Count}, sin proyecto {sinProyecto.Count}");
        Assert.IsTrue(barrios.Count > 0 && empresas.Count > 0 && lineas.Count > 0);
        Assert.IsTrue(sinProyecto.Count <= obras.Count);
        Assert.IsTrue(ff.All(f => f.LineaGestion is not null), "toda FF tiene su línea cargada");
        Assert.IsTrue(await simples.ExisteNombreAsync<IVC.Data.Entities.Barrio>(barrios[0].Nombre!.ToLowerInvariant(), idExcluido: 0),
            "la unicidad corre con la collation de la base (sin distinguir mayúsculas)");
    }

    [TestMethod]
    public async Task Proyectos_SeLeenConObraYCatalogos()
    {
        var factory = Crear();
        if (factory is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var filas = await new ProyectoRepository(factory).GetAllAsync();
        Console.WriteLine($"Proyectos: {filas.Count}");
        Assert.IsTrue(filas.Count > 0);
        Assert.IsTrue(filas.All(p => p.Obra is not null && p.Obra.Area is not null && p.Obra.Empresa is not null));
        var sinLinea = filas.Count(p => p.LineaGestion is null);
        Console.WriteLine($"Proyectos sin línea de gestión (el legado los ocultaba con INNER JOIN): {sinLinea}");
    }

    private sealed class FilaLegado
    {
        public int ID { get; set; }
        public decimal? AUTORIZADO2026 { get; set; }
    }

    [TestMethod]
    public async Task Movimientos_AutorizadoNuevo_CoincideConLaConsultaDelLegado()
    {
        var factory = Crear();
        if (factory is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var servicio = new MovimientosService(new MovimientoRepository(factory), NullLogger<MovimientosService>.Instance);
        var nuestras = (await servicio.GetAllAsync()).ToDictionary(m => m.Id);

        await using var db = factory.CreateDbContext();
        var legado = await db.Database.SqlQuery<FilaLegado>($@"
            SELECT M.ID,
                   BD.AUTORIZADO2025 + ISNULL((SELECT SUM(M2.MOVIMIENTO) FROM MOVIMIENTOS_GESTION M2 WHERE M2.ID_BASE = M.ID_BASE), 0) AS AUTORIZADO2026
            FROM MOVIMIENTOS_GESTION M
            INNER JOIN OBRAS O ON M.ID_BASE = O.ID
            LEFT JOIN BD_PROYECTOS BD ON O.ID = BD.ID_BASE").ToListAsync();

        Console.WriteLine($"Movimientos: {nuestras.Count} (legado {legado.Count})");
        Assert.AreEqual(legado.Count, nuestras.Count);
        var diferencias = legado
            .Where(l => !nuestras.TryGetValue(l.ID, out var n) || n.Autorizado2026 != l.AUTORIZADO2026)
            .Select(l => $"{l.ID}: legado {l.AUTORIZADO2026} / nuevo {(nuestras.TryGetValue(l.ID, out var n) ? n.Autorizado2026 : null)}")
            .ToList();
        Assert.AreEqual(0, diferencias.Count, string.Join(Environment.NewLine, diferencias.Take(10)));
    }
}
