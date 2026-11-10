using IVC.Application.Identidad;
using IVC.Data;
using IVC.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// Los repositorios de identidad contra una base IVC real (sólo lectura). Verifica lo que
/// las reglas de permisos dan por sentado sobre los datos: que las áreas de
/// Redeterminaciones y Secretaría existen con los Id del legado y están mapeadas a Auth.Web
/// (si no, ninguna restricción por área se aplicaría nunca), que ABM_PLANI tiene a lo sumo
/// una fila y que el largo de UsuariosVinculados.Nombre coincide con el recorte del
/// repositorio. Corre sólo con IVC_TEST_CONNECTION (apuntar a IVC_TEST, nunca a
/// producción); sin ella queda Inconclusive.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class IdentidadIntegracionTests
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
    public async Task Areas_RedeterminacionesYSecretaria_ExistenYEstanMapeadasAAuthWeb()
    {
        var factory = Crear();
        if (factory is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        await using var db = factory.CreateDbContext();
        var areas = await db.Areas.AsNoTracking()
            .Where(a => a.Id == IvcAreaIds.Redeterminaciones || a.Id == IvcAreaIds.Secretaria)
            .ToDictionaryAsync(a => a.Id);

        Assert.IsTrue(areas.ContainsKey(IvcAreaIds.Redeterminaciones), "AREAS no tiene la fila 16 (Redeterminaciones).");
        Assert.IsTrue(areas.ContainsKey(IvcAreaIds.Secretaria), "AREAS no tiene la fila 19 (Secretaría).");
        Console.WriteLine($"16 = {areas[IvcAreaIds.Redeterminaciones].Nombre} (AuthAreaId {areas[IvcAreaIds.Redeterminaciones].AuthAreaId})");
        Console.WriteLine($"19 = {areas[IvcAreaIds.Secretaria].Nombre} (AuthAreaId {areas[IvcAreaIds.Secretaria].AuthAreaId})");

        foreach (var area in areas.Values)
            Assert.IsNotNull(area.AuthAreaId, $"AREAS.Id={area.Id} sin AuthAreaId: la restricción de esa área nunca aplicaría.");

        // El camino real del claim "area" a las áreas IVC: con el AuthAreaId de Secretaría
        // el repositorio tiene que devolver la 19 (y las demás áreas que compartan ese mapeo).
        var repo = new AreaRepository(factory);
        var resueltas = await repo.GetByAuthAreaIdsAsync([areas[IvcAreaIds.Secretaria].AuthAreaId!.Value]);
        CollectionAssert.Contains(resueltas.Select(a => a.Id).ToList(), IvcAreaIds.Secretaria);
        Console.WriteLine($"AuthAreaId {areas[IvcAreaIds.Secretaria].AuthAreaId} -> IVC [{string.Join(", ", resueltas.Select(a => $"{a.Id} {a.Nombre}"))}]");

        var sinMapeo = await db.Areas.AsNoTracking().CountAsync(a => a.AuthAreaId == null);
        Console.WriteLine($"Áreas sin AuthAreaId: {sinMapeo} de {await db.Areas.CountAsync()}");
    }

    [TestMethod]
    public async Task AbmPlani_TieneALoSumoUnaFila_YElRepositorioLaLee()
    {
        var factory = Crear();
        if (factory is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        await using var db = factory.CreateDbContext();
        var filas = await db.AbmPlani.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        Assert.IsTrue(filas.Count <= 1, $"ABM_PLANI tiene {filas.Count} filas; el legado y la app nueva sólo leen la primera.");

        var estado = await new AbmPlaniRepository(factory).GetAsync();
        var esperado = filas.Count == 0
            ? EstadoPlanificacion.Cerrado
            : new EstadoPlanificacion(filas[0].PlanificacionAbierta, filas[0].FormulacionAbierta);
        Assert.AreEqual(esperado, estado);
        Console.WriteLine($"ABM_PLANI: {filas.Count} fila(s); planificación {(estado.PlanificacionAbierta ? "abierta" : "cerrada")}, formulación {(estado.FormulacionAbierta ? "abierta" : "cerrada")}");
    }

    [TestMethod]
    public async Task UsuariosVinculados_ElLargoDeNombreCoincideConElRecorte_YElRepositorioLista()
    {
        var factory = Crear();
        if (factory is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        await using var db = factory.CreateDbContext();
        var largo = await db.Database
            .SqlQuery<int>($"SELECT CHARACTER_MAXIMUM_LENGTH AS [Value] FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'UsuariosVinculados' AND COLUMN_NAME = 'Nombre'")
            .SingleAsync();
        Assert.AreEqual(UsuarioVinculadoRepository.NombreMaxLength, largo,
            "UsuariosVinculados.Nombre no mide lo que recorta el repositorio.");

        var usuarios = await new UsuarioVinculadoRepository(factory).GetAllAsync();
        Assert.IsTrue(usuarios.All(u => !string.IsNullOrWhiteSpace(u.AuthUserId) && !string.IsNullOrWhiteSpace(u.Nombre)));
        Console.WriteLine($"UsuariosVinculados: {usuarios.Count} usuarios, {usuarios.Count(u => u.PlanificacionAbierta)} con override de planificación");
    }
}
