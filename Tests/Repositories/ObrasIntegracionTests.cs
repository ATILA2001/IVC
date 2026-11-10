using IVC.Application.Identidad;
using IVC.Data;
using IVC.Repositories.Implementations;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Repositories;

/// <summary>
/// Paridad de los indicadores de Obras contra una base IVC real (sólo lectura): los
/// montos, fechas y ejecución física que calcula la app nueva se comparan fila por fila
/// con las mismas fórmulas escritas en SQL sobre AUTORIZANTES, CERTIFICADOS y
/// LEGITIMOS_ABONOS, para todas las obras. Corre sólo con IVC_TEST_CONNECTION.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class ObrasIntegracionTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private sealed class FilaLegado
    {
        public int ID { get; set; }
        public decimal SumAut { get; set; }
        public decimal SumAut4 { get; set; }
        public decimal SumLeg { get; set; }
        public decimal SumCert { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? EjecFisica { get; set; }
    }

    [TestMethod]
    public async Task IndicadoresDeTodasLasObras_CoincidenConLasFormulasDelLegadoEnSql()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }
        var factory = new Factory(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options);

        var admin = new UsuarioIvc(true, "test", "TEST", null, true, Array.Empty<int>(), Array.Empty<int>(), null, null, false);
        var usuarioActual = new Mock<IUsuarioActual>();
        usuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        var servicio = new ObrasService(new ObraRepository(factory), new LookupRepository(factory), usuarioActual.Object,
            Mock.Of<IPermisosIvc>(), NullLogger<ObrasService>.Instance);

        var nuestras = (await servicio.GetAllAsync()).ToDictionary(o => o.Id);

        await using var db = factory.CreateDbContext();
        var legado = await db.Database.SqlQuery<FilaLegado>($@"
            SELECT O.ID,
                   (SELECT COALESCE(SUM(A.MONTO_AUTORIZADO), 0) FROM AUTORIZANTES A WHERE A.OBRA = O.ID) AS SumAut,
                   (SELECT COALESCE(SUM(A.MONTO_AUTORIZADO), 0) FROM AUTORIZANTES A WHERE A.OBRA = O.ID AND A.CONCEPTO = 4) AS SumAut4,
                   (SELECT COALESCE(SUM(L.CERTIFICADO), 0) FROM LEGITIMOS_ABONOS L WHERE L.OBRA = O.ID) AS SumLeg,
                   (SELECT COALESCE(SUM(C.MONTO_TOTAL), 0) FROM CERTIFICADOS C INNER JOIN AUTORIZANTES A2 ON C.CODIGO_AUTORIZANTE = A2.CODIGO_AUTORIZANTE WHERE A2.OBRA = O.ID) AS SumCert,
                   (SELECT MIN(F) FROM (SELECT MIN(C.MES_APROBACION) AS F FROM CERTIFICADOS C INNER JOIN AUTORIZANTES A2 ON C.CODIGO_AUTORIZANTE = A2.CODIGO_AUTORIZANTE WHERE A2.OBRA = O.ID
                                        UNION ALL SELECT MIN(L.MES_APROBACION) FROM LEGITIMOS_ABONOS L WHERE L.OBRA = O.ID) X) AS FechaInicio,
                   (SELECT MAX(F) FROM (SELECT MAX(C.MES_APROBACION) AS F FROM CERTIFICADOS C INNER JOIN AUTORIZANTES A2 ON C.CODIGO_AUTORIZANTE = A2.CODIGO_AUTORIZANTE WHERE A2.OBRA = O.ID
                                        UNION ALL SELECT MAX(L.MES_APROBACION) FROM LEGITIMOS_ABONOS L WHERE L.OBRA = O.ID) X) AS FechaFin,
                   (SELECT CASE WHEN COALESCE(SUM(A.MONTO_AUTORIZADO), 0) = 0 THEN NULL
                                ELSE SUM(A.MONTO_AUTORIZADO * COALESCE(E.S, 0)) / SUM(A.MONTO_AUTORIZADO) END
                    FROM AUTORIZANTES A
                    LEFT JOIN (SELECT CODIGO_AUTORIZANTE, SUM(PORC_EJEC_FISICA) AS S FROM CERTIFICADOS WHERE PORC_EJEC_FISICA IS NOT NULL GROUP BY CODIGO_AUTORIZANTE) E
                           ON E.CODIGO_AUTORIZANTE = A.CODIGO_AUTORIZANTE
                    WHERE A.OBRA = O.ID) AS EjecFisica
            FROM OBRAS O").ToListAsync();

        Console.WriteLine($"Obras: {nuestras.Count} (SQL {legado.Count})");
        Assert.AreEqual(legado.Count, nuestras.Count);

        var diferencias = new List<string>();
        foreach (var l in legado)
        {
            if (!nuestras.TryGetValue(l.ID, out var n)) { diferencias.Add($"{l.ID}: falta en la app"); continue; }
            Comparar(diferencias, l.ID, "MontoCertificado", l.SumCert + l.SumLeg, n.MontoCertificado);
            Comparar(diferencias, l.ID, "MontoInicial", l.SumAut4 + l.SumLeg, n.MontoInicial);
            Comparar(diferencias, l.ID, "MontoActual", l.SumAut + l.SumLeg, n.MontoActual);
            Comparar(diferencias, l.ID, "MontoFaltante", (l.SumAut + l.SumLeg) - (l.SumCert + l.SumLeg), n.MontoFaltante);
            if (l.FechaInicio != n.FechaInicio) diferencias.Add($"{l.ID}: FechaInicio {l.FechaInicio:d} / {n.FechaInicio:d}");
            if (l.FechaFin != n.FechaFin) diferencias.Add($"{l.ID}: FechaFin {l.FechaFin:d} / {n.FechaFin:d}");
            Comparar(diferencias, l.ID, "EjecFisica", l.EjecFisica, n.EjecFisica, tolerancia: 0.01m);
        }
        Assert.AreEqual(0, diferencias.Count, string.Join(Environment.NewLine, diferencias.Take(15)));
    }

    [TestMethod]
    public async Task FiltrosPorAreaYPorObra_SeTraducenASql_YCoincidenConElTotal()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }
        var repo = new ObraRepository(new Factory(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options));

        var todas = await repo.GetAllAsync(null);
        var areaConObras = todas.GroupBy(o => o.AreaId).OrderByDescending(g => g.Count()).First().Key;
        var deUnArea = await repo.GetAllAsync([areaConObras]);
        Assert.IsTrue(deUnArea.Count > 0 && deUnArea.All(o => o.AreaId == areaConObras), "el filtro por área corre en SQL");
        Assert.AreEqual(todas.Count(o => o.AreaId == areaConObras), deUnArea.Count);
        Console.WriteLine($"Área {areaConObras}: {deUnArea.Count} de {todas.Count} obras");

        // Los agregados pedidos para un subconjunto de obras tienen que ser los mismos que
        // los calculados sobre todas: es el camino de los usuarios de un área y de la
        // relectura de una fila tras guardar.
        var completos = await repo.GetAgregadosAsync(null);
        var ids = deUnArea.Select(o => o.Id).Take(25).ToList();
        var parciales = await repo.GetAgregadosAsync(ids);
        foreach (var id in ids)
        {
            var hayDatos = completos.TryGetValue(id, out var esperado);
            Assert.AreEqual(hayDatos, parciales.ContainsKey(id), $"obra {id}");
            if (!hayDatos) continue;
            var obtenido = parciales[id];
            Assert.AreEqual(esperado!.SumaAutorizantes, obtenido.SumaAutorizantes, $"obra {id} autorizantes");
            Assert.AreEqual(esperado.SumaCertificados, obtenido.SumaCertificados, $"obra {id} certificados");
            Assert.AreEqual(esperado.SumaLegitimos, obtenido.SumaLegitimos, $"obra {id} legítimos");
            Assert.AreEqual(esperado.Autorizantes.Count, obtenido.Autorizantes.Count, $"obra {id} ejecución");
        }
        var una = await repo.GetAgregadosAsync([ids[0]]);
        Assert.AreEqual(completos.ContainsKey(ids[0]), una.ContainsKey(ids[0]));
    }

    private static void Comparar(List<string> diferencias, int id, string campo, decimal? esperado, decimal? obtenido, decimal tolerancia = 0.005m)
    {
        if (esperado is null && obtenido is null) return;
        if (esperado is null || obtenido is null || Math.Abs(esperado.Value - obtenido.Value) > tolerancia)
            diferencias.Add($"{id}: {campo} legado {esperado} / nuevo {obtenido}");
    }
}
