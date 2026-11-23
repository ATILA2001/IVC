using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// El repositorio de formulación sobre la base en memoria: la ventana del ciclo, el filtro
/// por área, el upsert/borrado por año de GuardarPivot, la baja por obra y el lookup de
/// obras sin formulación.
/// </summary>
[TestClass]
public class FormulacionRepositoryTests
{
    private static readonly int[] Ciclo = [2027, 2028, 2029];

    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private static async Task<Factory> SembrarAsync()
    {
        var opciones = new DbContextOptionsBuilder<IvcDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new IvcDbContext(opciones);
        db.Areas.AddRange(new Area { Id = 1, Nombre = "A1" }, new Area { Id = 2, Nombre = "A2" });
        db.Empresas.Add(new Empresa { Id = 1, Nombre = "E" });
        db.UnidadesMedida.Add(new UnidadMedida { Id = 1, Nombre = "VIVIENDAS" });
        db.Prioridades.Add(new Prioridad { Id = 2, Nombre = "ALTA" });
        db.Obras.AddRange(
            new Obra { Id = 10, Descripcion = "OBRA B", AreaId = 1, EmpresaId = 1 },
            new Obra { Id = 20, Descripcion = "OBRA A", AreaId = 2, EmpresaId = 1 },
            new Obra { Id = 30, Descripcion = "OBRA SIN FORMULACION", AreaId = 1, EmpresaId = 1 },
            new Obra { Id = 40, Descripcion = "OBRA DEL CICLO VIEJO", AreaId = 1, EmpresaId = 1 });
        db.Formulaciones.AddRange(
            new Formulacion { Id = 1, ObraId = 10, FechaPeriodo = new DateTime(2027, 1, 1), Monto = 100m, UnidadMedidaId = 1, PrioridadId = 2, Ppi = 1 },
            new Formulacion { Id = 2, ObraId = 10, FechaPeriodo = new DateTime(2029, 1, 1), Monto = 300m, UnidadMedidaId = 1, PrioridadId = 2, Ppi = 1 },
            new Formulacion { Id = 3, ObraId = 20, FechaPeriodo = new DateTime(2028, 1, 1), Monto = 50m },
            new Formulacion { Id = 4, ObraId = 40, FechaPeriodo = new DateTime(2026, 1, 1), Monto = 999m });
        await db.SaveChangesAsync();
        return new Factory(opciones);
    }

    [TestMethod]
    public async Task GetDelCiclo_SoloLosAniosDelCiclo_PorArea_OrdenadoPorObra()
    {
        var repo = new FormulacionRepository(await SembrarAsync());

        var todas = await repo.GetDelCicloAsync(Ciclo, null);
        CollectionAssert.AreEqual(new[] { 20, 10, 10 }, todas.Select(f => f.ObraId).ToList(), "por descripción de obra y luego por año; la de 2026 queda fuera");
        Assert.AreEqual("VIVIENDAS", todas.Last().UnidadMedida?.Nombre);
        Assert.AreEqual("A1", todas.Last().Obra.Area?.Nombre);

        Assert.AreEqual(1, (await repo.GetDelCicloAsync(Ciclo, [2])).Count);
        Assert.AreEqual(0, (await repo.GetDelCicloAsync(Ciclo, [])).Count);
        Assert.AreEqual(0, (await repo.GetDelCicloAsync([], null)).Count);

        Assert.AreEqual(2, (await repo.GetDeObraAsync(10, Ciclo)).Count);
        Assert.AreEqual(0, (await repo.GetDeObraAsync(40, Ciclo)).Count, "la formulación del ciclo viejo no se ve");
    }

    [TestMethod]
    public async Task GuardarPivot_InsertaActualizaYBorraPorAnio_YReplicaLosCompartidos()
    {
        var factory = await SembrarAsync();
        var repo = new FormulacionRepository(factory);
        var compartido = new Formulacion { Ppi = 5, Techos = 800m, UnidadMedidaId = 1, ValorMedida = 12m, PrioridadId = 2, BreveDescripcion = "NUEVA", MesBase = new DateTime(2026, 6, 1) };

        // 2027 se actualiza, 2028 se inserta, 2029 se elimina.
        await repo.GuardarPivotAsync(10, Ciclo, compartido, [150m, 20m, null]);

        var filas = await repo.GetDeObraAsync(10, Ciclo);
        CollectionAssert.AreEqual(new[] { 2027, 2028 }, filas.Select(f => f.FechaPeriodo.Year).ToList());
        Assert.AreEqual(150m, filas[0].Monto);
        Assert.AreEqual(1, filas[0].Id, "la fila existente se actualiza, no se recrea");
        Assert.AreEqual(20m, filas[1].Monto);
        Assert.AreEqual(new DateTime(2028, 1, 1), filas[1].FechaPeriodo);
        Assert.IsTrue(filas.All(f => f.Ppi == 5 && f.Techos == 800m && f.BreveDescripcion == "NUEVA" && f.MesBase == new DateTime(2026, 6, 1)), "compartidos iguales en todas");

        await using var db = factory.CreateDbContext();
        Assert.AreEqual(999m, (await db.Formulaciones.SingleAsync(f => f.ObraId == 40)).Monto, "lo de otros ciclos no se toca");

        // Alta de una obra sin formulación: sólo los años con monto.
        await repo.GuardarPivotAsync(30, Ciclo, compartido, [null, null, 7m]);
        var nuevas = await repo.GetDeObraAsync(30, Ciclo);
        Assert.AreEqual(2029, nuevas.Single().FechaPeriodo.Year);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => repo.GuardarPivotAsync(30, Ciclo, compartido, [1m]));
    }

    [TestMethod]
    public async Task EliminarDeObra_BorraSoloElCiclo_YDiceSiHabiaAlgo()
    {
        var factory = await SembrarAsync();
        var repo = new FormulacionRepository(factory);

        Assert.IsTrue(await repo.EliminarDeObraAsync(10, Ciclo));
        Assert.IsFalse(await repo.EliminarDeObraAsync(10, Ciclo), "ya no queda nada");
        Assert.IsFalse(await repo.EliminarDeObraAsync(40, Ciclo), "la del ciclo viejo no entra en la ventana");

        await using var db = factory.CreateDbContext();
        Assert.AreEqual(2, await db.Formulaciones.CountAsync(), "quedan la de la obra 20 y la del ciclo viejo");
    }

    [TestMethod]
    public async Task Lookups_ObrasSinFormulacionEnElCiclo_UnidadesYPrioridades()
    {
        var repo = new LookupRepository(await SembrarAsync());

        var sinFormulacion = await repo.GetObrasSinFormulacionAsync(Ciclo, null);
        CollectionAssert.AreEquivalent(new[] { 30, 40 }, sinFormulacion.Select(o => o.Id).ToList(), "la del ciclo viejo cuenta como disponible");
        Assert.AreEqual(2, (await repo.GetObrasSinFormulacionAsync(Ciclo, [1])).Count);
        Assert.AreEqual(0, (await repo.GetObrasSinFormulacionAsync(Ciclo, [2])).Count, "la obra 20 ya está formulada");
        Assert.AreEqual(0, (await repo.GetObrasSinFormulacionAsync(Ciclo, [])).Count);

        Assert.AreEqual("VIVIENDAS", (await repo.GetUnidadesMedidaAsync()).Single().Nombre);
        Assert.AreEqual("ALTA", (await repo.GetPrioridadesAsync()).Single().Nombre);
    }
}
