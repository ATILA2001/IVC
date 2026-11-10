using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// Los agregados por obra sobre la base en memoria: sumas de autorizantes (total y
/// concepto 4), legítimos, certificados vinculados por código de autorizante y la
/// ejecución física por autorizante; y el filtro por área del listado.
/// </summary>
[TestClass]
public class ObraRepositoryTests
{
    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private static async Task<DbContextOptions<IvcDbContext>> SembrarAsync()
    {
        var opciones = new DbContextOptionsBuilder<IvcDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new IvcDbContext(opciones);
        db.Areas.AddRange(new Area { Id = 1, Nombre = "A1" }, new Area { Id = 2, Nombre = "A2" });
        db.Empresas.Add(new Empresa { Id = 1, Nombre = "E" });
        db.Obras.AddRange(
            new Obra { Id = 10, Descripcion = "OBRA 10", AreaId = 1, EmpresaId = 1 },
            new Obra { Id = 20, Descripcion = "OBRA 20", AreaId = 2, EmpresaId = 1 },
            new Obra { Id = 30, Descripcion = "OBRA 30", AreaId = 1, EmpresaId = 1 });
        db.Autorizantes.AddRange(
            new Autorizante { Id = 1, ObraId = 10, CodigoAutorizante = "A-1", ConceptoId = 4, MontoAutorizado = 600m },
            new Autorizante { Id = 2, ObraId = 10, CodigoAutorizante = "A-2", ConceptoId = 7, MontoAutorizado = 400m },
            new Autorizante { Id = 3, ObraId = 20, CodigoAutorizante = "B-1", ConceptoId = 4, MontoAutorizado = 50m });
        db.Certificados.AddRange(
            new Certificado { Id = 1, CodigoAutorizante = "A-1", MontoTotal = 200m, MesAprobacion = new DateTime(2025, 5, 1), PorcEjecFisica = 30m },
            new Certificado { Id = 2, CodigoAutorizante = "A-1", MontoTotal = 100m, MesAprobacion = new DateTime(2026, 1, 1), PorcEjecFisica = 20m },
            new Certificado { Id = 3, CodigoAutorizante = "A-2", MontoTotal = 50m, MesAprobacion = new DateTime(2025, 8, 1), PorcEjecFisica = null },
            new Certificado { Id = 4, CodigoAutorizante = "Z-9", MontoTotal = 999m, MesAprobacion = new DateTime(2020, 1, 1) }); // sin autorizante: no cuenta
        db.Legitimos.AddRange(
            new Legitimo { Id = 1, ObraId = 10, CodigoAutorizante = "L-1", Certificado = 100m, MesAprobacion = new DateTime(2025, 3, 1) });
        await db.SaveChangesAsync();
        return opciones;
    }

    [TestMethod]
    public async Task Agregados_DeTodasLasObras_SumanYVinculanCertificadosPorCodigo()
    {
        var repo = new ObraRepository(new Factory(await SembrarAsync()));

        var agregados = await repo.GetAgregadosAsync(null);

        var a10 = agregados[10];
        Assert.AreEqual(1000m, a10.SumaAutorizantes);
        Assert.AreEqual(600m, a10.SumaAutorizantesConcepto4);
        Assert.AreEqual(100m, a10.SumaLegitimos);
        Assert.AreEqual(350m, a10.SumaCertificados, "sólo los certificados cuyos códigos son de la obra");
        Assert.AreEqual(new DateTime(2025, 5, 1), a10.MinCertificado);
        Assert.AreEqual(new DateTime(2026, 1, 1), a10.MaxCertificado);
        Assert.AreEqual(new DateTime(2025, 3, 1), a10.MinLegitimo);
        CollectionAssert.AreEquivalent(
            new[] { (600m, 50m), (400m, 0m) },
            a10.Autorizantes.Select(x => (x.MontoAutorizado, x.EjecFisica)).ToArray(),
            "A-1 acumula 30 + 20; A-2 no tiene ejecución cargada");

        Assert.AreEqual(50m, agregados[20].SumaAutorizantes);
        Assert.AreEqual(0m, agregados[20].SumaCertificados);
        Assert.IsFalse(agregados.ContainsKey(30), "sin movimientos no aparece");
    }

    [TestMethod]
    public async Task Agregados_FiltradosPorObra_YListado_FiltradoPorArea()
    {
        var repo = new ObraRepository(new Factory(await SembrarAsync()));

        var soloVeinte = await repo.GetAgregadosAsync([20]);
        Assert.IsTrue(soloVeinte.ContainsKey(20) && !soloVeinte.ContainsKey(10));
        Assert.AreEqual(0, (await repo.GetAgregadosAsync(Array.Empty<int>())).Count);

        var area1 = await repo.GetAllAsync([1]);
        CollectionAssert.AreEqual(new[] { 10, 30 }, area1.Select(o => o.Id).ToArray(), "ordenadas por descripción");
        Assert.AreEqual("A1", area1[0].Area.Nombre, "los catálogos vienen cargados");
        Assert.AreEqual(3, (await repo.GetAllAsync(null)).Count);
        Assert.AreEqual(0, (await repo.GetAllAsync(Array.Empty<int>())).Count);
    }
}
