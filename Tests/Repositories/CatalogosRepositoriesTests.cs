using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// Repositorios de los catálogos sobre la base en memoria: consultas, navegaciones
/// cargadas y los cálculos que las páginas dan por hechos (obras sin proyecto, suma de
/// movimientos por obra, texto del desplegable de obras).
/// </summary>
[TestClass]
public class CatalogosRepositoriesTests
{
    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private static DbContextOptions<IvcDbContext> Opciones() =>
        new DbContextOptionsBuilder<IvcDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    /// <summary>Dos obras con sus catálogos; la primera con proyecto y dos movimientos, la segunda sin nada.</summary>
    private static async Task SembrarObrasAsync(DbContextOptions<IvcDbContext> opciones)
    {
        await using var db = new IvcDbContext(opciones);
        db.Barrios.Add(new Barrio { Id = 1, Nombre = "LUGANO" });
        db.Empresas.Add(new Empresa { Id = 1, Nombre = "CONSTRUCTORA SA" });
        db.Contratas.Add(new Contrata { Id = 1, Nombre = "LP" });
        db.Areas.Add(new Area { Id = 1, Nombre = "OBRAS" });
        db.LineasGestion.Add(new LineaGestion { Id = 1, Nombre = "VIVIENDA", Tipo = "T", Grupo = "G", Reparticion = "R" });
        db.Obras.AddRange(
            new Obra { Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024, BarrioId = 1, EmpresaId = 1, ContrataId = 1, AreaId = 1 },
            new Obra { Id = 20, Descripcion = "TORRE B", Numero = 7, Anio = 2025, BarrioId = 1, EmpresaId = 1, ContrataId = 1, AreaId = 1 });
        db.Proyectos.Add(new Proyecto { Id = 100, ObraId = 10, Nombre = "PROY", SubProyecto = "SUB", LineaGestionId = 1, Autorizado2025 = 1000m, Autorizado2026 = 1500m });
        db.MovimientosGestion.AddRange(
            new MovimientoGestion { Id = 1, ObraId = 10, Monto = 100m, Fecha = new DateTime(2026, 1, 1) },
            new MovimientoGestion { Id = 2, ObraId = 10, Monto = 50.5m, Fecha = new DateTime(2026, 2, 1) });
        await db.SaveChangesAsync();
    }

    [TestMethod]
    public async Task CatalogoSimple_Crud_YExisteNombre_SobreCualquierEntidad()
    {
        var opciones = Opciones();
        var repo = new CatalogoSimpleRepository(new Factory(opciones));

        var barrio = new Barrio { Nombre = "Lugano" };
        await repo.AddAsync(barrio);
        await repo.AddAsync(new Barrio { Nombre = "Soldati" });
        await repo.AddAsync(new Empresa { Nombre = "Lugano" }); // mismo nombre en otra tabla: no interfiere

        var barrios = await repo.GetAllAsync<Barrio>();
        CollectionAssert.AreEqual(new[] { "Lugano", "Soldati" }, barrios.Select(b => b.Nombre).ToArray(), "ordenados por nombre");
        Assert.AreEqual(1, (await repo.GetAllAsync<Empresa>()).Count);

        Assert.IsTrue(await repo.ExisteNombreAsync<Barrio>("Soldati", idExcluido: barrio.Id));
        Assert.IsFalse(await repo.ExisteNombreAsync<Barrio>("Lugano", idExcluido: barrio.Id), "la propia fila no cuenta");

        barrio.Nombre = "Villa Lugano";
        await repo.UpdateAsync(barrio);
        Assert.AreEqual("Villa Lugano", (await repo.GetByIdAsync<Barrio>(barrio.Id))!.Nombre);

        await repo.DeleteAsync<Barrio>(barrio.Id);
        await repo.DeleteAsync<Barrio>(999); // inexistente: no falla
        Assert.AreEqual(1, (await repo.GetAllAsync<Barrio>()).Count);
    }

    [TestMethod]
    public async Task LineasFF_SeListanConSuLinea_YLineas_ExisteNombre()
    {
        var opciones = Opciones();
        await SembrarObrasAsync(opciones);
        var lineas = new LineaGestionRepository(new Factory(opciones));
        var ff = new LineaGestionFFRepository(new Factory(opciones));

        await ff.AddAsync(new LineaGestionFF { LineaGestionId = 1, Nombre = "FF 11", Fuente = "Tesoro" });

        var fila = (await ff.GetAllAsync()).Single();
        Assert.AreEqual("VIVIENDA", fila.LineaGestion.Nombre);
        Assert.AreEqual("R", fila.LineaGestion.Reparticion);
        Assert.IsTrue(await lineas.ExisteNombreAsync("VIVIENDA", idExcluido: 0));
        Assert.IsFalse(await lineas.ExisteNombreAsync("VIVIENDA", idExcluido: 1));
    }

    [TestMethod]
    public async Task Proyectos_SeListanConObraYCatalogos_YExisteParaObra()
    {
        var opciones = Opciones();
        await SembrarObrasAsync(opciones);
        var repo = new ProyectoRepository(new Factory(opciones));

        var proyecto = (await repo.GetAllAsync()).Single();
        Assert.AreEqual("TORRE A", proyecto.Obra.Descripcion);
        Assert.AreEqual("LUGANO", proyecto.Obra.Barrio!.Nombre);
        Assert.AreEqual("LP", proyecto.Obra.Contrata!.Nombre);
        Assert.AreEqual("OBRAS", proyecto.Obra.Area.Nombre);
        Assert.AreEqual("CONSTRUCTORA SA", proyecto.Obra.Empresa.Nombre);
        Assert.AreEqual("VIVIENDA", proyecto.LineaGestion!.Nombre);

        Assert.IsTrue(await repo.ExisteParaObraAsync(10));
        Assert.IsFalse(await repo.ExisteParaObraAsync(20));
    }

    [TestMethod]
    public async Task Movimientos_SeListanConObraYProyecto_YSeSumanPorObra()
    {
        var opciones = Opciones();
        await SembrarObrasAsync(opciones);
        var repo = new MovimientoRepository(new Factory(opciones));

        var movimientos = await repo.GetAllAsync();
        Assert.AreEqual(2, movimientos.Count);
        Assert.AreEqual("PROY", movimientos[0].Obra.Proyecto!.Nombre);
        Assert.AreEqual("VIVIENDA", movimientos[0].Obra.Proyecto!.LineaGestion!.Nombre);

        Assert.AreEqual(150.5m, await repo.SumarMontoPorObraAsync(10));
        Assert.IsTrue((await repo.SumarMontoPorObraAsync(20) ?? 0m) == 0m, "sin movimientos: nada que sumar");
    }

    [TestMethod]
    public async Task Lookup_ObrasSinProyecto_ExcluyeLasQueYaTienen_YArmaElTextoDelLegado()
    {
        var opciones = Opciones();
        await SembrarObrasAsync(opciones);
        var repo = new LookupRepository(new Factory(opciones));

        var todas = await repo.GetObrasAsync();
        var sinProyecto = await repo.GetObrasSinProyectoAsync();
        var lineas = await repo.GetLineasGestionAsync();

        Assert.AreEqual(2, todas.Count);
        Assert.AreEqual("LUGANO - TORRE A - LP - 12/2024", todas.Single(o => o.Id == 10).Nombre);
        Assert.AreEqual(20, sinProyecto.Single().Id);
        Assert.AreEqual("LUGANO - TORRE B - CONSTRUCTORA SA - LP - 7/2025", sinProyecto.Single().Nombre, "el alta de proyecto muestra también la empresa");
        Assert.AreEqual("VIVIENDA", lineas.Single().Nombre);
    }

    [TestMethod]
    public void TextoObra_OmiteLasPartesSinDato()
    {
        var obra = new Obra { Descripcion = "SOLA", Barrio = null, Contrata = null, Numero = null, Anio = null };
        Assert.AreEqual("SOLA", LookupRepository.TextoObra(obra, conEmpresa: true));
    }
}
