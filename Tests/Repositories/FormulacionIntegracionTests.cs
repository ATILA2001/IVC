using IVC.Application.Formulacion;
using IVC.Application.Identidad;
using IVC.Data;
using IVC.Repositories.Implementations;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Repositories;

/// <summary>
/// Paridad del pivot de Formulación contra una base IVC real (sólo lectura): una fila por
/// obra con formulación en el ciclo vigente, con los montos de cada año y la suma total,
/// comparada con la misma agrupación escrita en SQL sobre FORMULACION (lo que mostraba la
/// grilla y el subtotal del legado). Corre sólo con IVC_TEST_CONNECTION.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class FormulacionIntegracionTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private sealed class FilaSql
    {
        public int ObraId { get; set; }
        public int Anio { get; set; }
        public decimal? Monto { get; set; }
        public int? Ppi { get; set; }
        public int? UnidadMedidaId { get; set; }
    }

    [TestMethod]
    public async Task PivotDeTodasLasObras_CoincideConLaAgrupacionEnSql()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }
        var factory = new Factory(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options);

        var admin = new UsuarioIvc(true, "test", "TEST", null, true, Array.Empty<int>(), Array.Empty<int>(), null, null, false);
        var usuarioActual = new Mock<IUsuarioActual>();
        usuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        var opciones = Options.Create(new FormulacionOptions { AnioBase = FormulacionOptions.AnioBasePorDefecto });
        var servicio = new FormulacionService(new FormulacionRepository(factory), new ObraRepository(factory), new LookupRepository(factory),
            usuarioActual.Object, Mock.Of<IPermisosIvc>(), opciones, NullLogger<FormulacionService>.Instance);
        var ciclo = servicio.Ciclo;

        var nuestras = (await servicio.GetAllAsync()).ToDictionary(f => f.ObraId);

        await using var db = factory.CreateDbContext();
        var sql = await db.Database.SqlQuery<FilaSql>($@"
            SELECT F.ID_BASE AS ObraId, YEAR(F.FECHA_PERIODO) AS Anio, F.MONTO AS Monto, F.PPI AS Ppi, F.ID_UNIDAD_MEDIDA AS UnidadMedidaId
            FROM FORMULACION F
            WHERE YEAR(F.FECHA_PERIODO) BETWEEN {ciclo.Anios[0]} AND {ciclo.Anios[2]}").ToListAsync();
        var porObra = sql.GroupBy(f => f.ObraId).ToDictionary(g => g.Key, g => g.ToList());

        Console.WriteLine($"Ciclo {ciclo.Anios[0]}-{ciclo.Anios[2]}: obras con formulación {nuestras.Count} (SQL {porObra.Count}); filas SQL {sql.Count}; " +
                          $"total {nuestras.Values.Sum(f => f.TotalMonto):N2} (SQL {sql.Sum(f => f.Monto ?? 0m):N2})");
        Assert.AreEqual(porObra.Count, nuestras.Count, "una fila por obra con formulación en el ciclo");

        var diferencias = new List<string>();
        foreach (var (obraId, filas) in porObra)
        {
            if (!nuestras.TryGetValue(obraId, out var vm)) { diferencias.Add($"obra {obraId}: falta"); continue; }
            foreach (var fila in filas)
            {
                var esperado = fila.Monto;
                var nuestro = ciclo.Posicion(fila.Anio) switch { 0 => vm.MontoAnio1, 1 => vm.MontoAnio2, 2 => vm.MontoAnio3, _ => null };
                if (nuestro != esperado) diferencias.Add($"obra {obraId} año {fila.Anio}: {nuestro} vs {esperado}");
            }
            if (vm.TotalMonto != filas.Sum(f => f.Monto ?? 0m)) diferencias.Add($"obra {obraId}: total {vm.TotalMonto} vs {filas.Sum(f => f.Monto ?? 0m)}");
            var primera = filas.OrderBy(f => f.Anio).First();
            if (vm.Ppi != primera.Ppi) diferencias.Add($"obra {obraId}: PPI {vm.Ppi} vs {primera.Ppi}");
            if (vm.UnidadMedidaId != primera.UnidadMedidaId) diferencias.Add($"obra {obraId}: unidad {vm.UnidadMedidaId} vs {primera.UnidadMedidaId}");
        }
        Assert.AreEqual(0, diferencias.Count, string.Join("\n", diferencias.Take(20)));

        // Las obras disponibles para el alta son exactamente las que no están en la grilla.
        var disponibles = await servicio.GetObrasDisponiblesAsync();
        var totalObras = await db.Obras.AsNoTracking().CountAsync();
        Console.WriteLine($"Obras: {totalObras}; disponibles para formular: {disponibles.Count}");
        Assert.AreEqual(totalObras, disponibles.Count + nuestras.Count);
        Assert.IsFalse(disponibles.Any(o => nuestras.ContainsKey(o.Id)));
    }
}
