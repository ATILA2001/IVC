using IVC.Application.Identidad;
using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// ABM_PLANI es una tabla de una sola fila que el legado creaba al primer uso, y
/// UsuariosVinculados se da de alta sola en el primer ingreso con el nombre recortado al
/// largo de la columna.
/// </summary>
[TestClass]
public class ConfiguracionRepositoriesTests
{
    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private static DbContextOptions<IvcDbContext> Opciones() =>
        new DbContextOptionsBuilder<IvcDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [TestMethod]
    public async Task AbmPlani_SinFilas_EstaCerrado_YElPrimerSetCreaLaFila()
    {
        var opciones = Opciones();
        var repo = new AbmPlaniRepository(new Factory(opciones));

        Assert.AreEqual(EstadoPlanificacion.Cerrado, await repo.GetAsync());

        await repo.SetAsync(new EstadoPlanificacion(true, false));
        await repo.SetAsync(new EstadoPlanificacion(true, true));

        Assert.AreEqual(new EstadoPlanificacion(true, true), await repo.GetAsync());
        await using var db = new IvcDbContext(opciones);
        Assert.AreEqual(1, await db.AbmPlani.CountAsync(), "siempre una sola fila");
    }

    [TestMethod]
    public async Task UsuarioVinculado_PrimerIngreso_CreaLaFila_YLuegoSincronizaElNombre()
    {
        var opciones = Opciones();
        var repo = new UsuarioVinculadoRepository(new Factory(opciones));

        var creado = await repo.GetOrCreateAsync("u1", "Ana");
        Assert.AreEqual("Ana", creado.Nombre);
        Assert.IsFalse(creado.PlanificacionAbierta);

        var renombrado = await repo.GetOrCreateAsync("u1", "Ana María");
        Assert.AreEqual("Ana María", renombrado.Nombre);

        await using var db = new IvcDbContext(opciones);
        Assert.AreEqual(1, await db.UsuariosVinculados.CountAsync());
    }

    [TestMethod]
    public async Task UsuarioVinculado_NombreLargo_SeRecortaAlLargoDeLaColumna()
    {
        var repo = new UsuarioVinculadoRepository(new Factory(Opciones()));
        var largo = new string('x', 80);

        var fila = await repo.GetOrCreateAsync("u1", largo);

        Assert.AreEqual(UsuarioVinculadoRepository.NombreMaxLength, fila.Nombre.Length);
    }

    [TestMethod]
    public async Task UsuarioVinculado_SetOverride_PersisteYDevuelveFalseSiNoExiste()
    {
        var opciones = Opciones();
        var repo = new UsuarioVinculadoRepository(new Factory(opciones));
        await repo.GetOrCreateAsync("u1", "Ana");

        Assert.IsTrue(await repo.SetPlanificacionAbiertaAsync("u1", true));
        Assert.IsFalse(await repo.SetPlanificacionAbiertaAsync("no-existe", true));

        var todos = await repo.GetAllAsync();
        Assert.IsTrue(todos.Single().PlanificacionAbierta);
    }

    [TestMethod]
    public async Task Areas_PorAuthAreaId_DevuelveSoloLasMapeadas_OrdenadasPorId()
    {
        var opciones = Opciones();
        await using (var db = new IvcDbContext(opciones))
        {
            db.Areas.AddRange(
                new Area { Id = 20, Nombre = "C", AuthAreaId = 36 },
                new Area { Id = 18, Nombre = "A", AuthAreaId = 36 },
                new Area { Id = 16, Nombre = "REDET", AuthAreaId = 40 },
                new Area { Id = 5, Nombre = "SIN MAPEO", AuthAreaId = null });
            await db.SaveChangesAsync();
        }
        var repo = new AreaRepository(new Factory(opciones));

        var areas = await repo.GetByAuthAreaIdsAsync([36]);

        CollectionAssert.AreEqual(new[] { 18, 20 }, areas.Select(a => a.Id).ToArray());
        Assert.AreEqual(0, (await repo.GetByAuthAreaIdsAsync([])).Count);
    }
}
