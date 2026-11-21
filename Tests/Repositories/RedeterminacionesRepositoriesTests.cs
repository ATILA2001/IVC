using IVC.Data;
using IVC.Data.Entities;
using IVC.Repositories.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Repositories;

/// <summary>
/// Los repositorios del módulo de redeterminaciones sobre la base en memoria: filtros por
/// área, dependencias y sumas del autorizante, ejecución física por código, seguimiento
/// inline, expedientes de reliquidación por año/mes y los lookups nuevos.
/// </summary>
[TestClass]
public class RedeterminacionesRepositoriesTests
{
    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private static async Task<Factory> SembrarAsync()
    {
        var opciones = new DbContextOptionsBuilder<IvcDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new IvcDbContext(opciones);
        db.Areas.AddRange(new Area { Id = 1, Nombre = "A1" }, new Area { Id = 2, Nombre = "A2" }, new Area { Id = 16, Nombre = "REDET" });
        db.Empresas.Add(new Empresa { Id = 1, Nombre = "E" });
        db.Conceptos.Add(new Concepto { Id = 4, Nombre = "BASICO" });
        db.EstadosAutorizante.Add(new EstadoAutorizante { Id = 1, Nombre = "EN EJECUCION" });
        db.EstadosRedet.Add(new EstadoRedet { Id = 12, Nombre = "RD-11/11-Notificada" });
        db.Usuarios.AddRange(
            new Usuario { Id = 9, Nombre = "ANA", AreaId = 16, Estado = true },
            new Usuario { Id = 8, Nombre = "BOB", AreaId = 16, Estado = false },
            new Usuario { Id = 7, Nombre = "CAR", AreaId = 1, Estado = true });
        db.Obras.AddRange(
            new Obra { Id = 10, Descripcion = "OBRA 10", AreaId = 1, EmpresaId = 1 },
            new Obra { Id = 20, Descripcion = "OBRA 20", AreaId = 2, EmpresaId = 1 });
        db.Autorizantes.AddRange(
            new Autorizante { Id = 1, ObraId = 10, CodigoAutorizante = "A-1", Detalle = "D1", ConceptoId = 4, EstadoId = 1, MontoAutorizado = 600m },
            new Autorizante { Id = 2, ObraId = 10, CodigoAutorizante = "A-2", Detalle = "D2", ConceptoId = 4, EstadoId = 1, MontoAutorizado = 400m },
            new Autorizante { Id = 3, ObraId = 20, CodigoAutorizante = "B-1", Detalle = "D3", ConceptoId = 4, EstadoId = 1, MontoAutorizado = 50m },
            new Autorizante { Id = 4, ObraId = 20, CodigoAutorizante = "C-1", Detalle = "D4", ConceptoId = 4, EstadoId = 1, MontoAutorizado = 10m });
        db.Certificados.AddRange(
            new Certificado { Id = 1, CodigoAutorizante = "A-1", MontoTotal = 200m, MesAprobacion = new DateTime(2025, 5, 1), PorcEjecFisica = 30m },
            new Certificado { Id = 2, CodigoAutorizante = "A-1", MontoTotal = 100m, MesAprobacion = new DateTime(2026, 1, 1), PorcEjecFisica = 20m },
            new Certificado { Id = 3, CodigoAutorizante = "A-2", MontoTotal = 50m, MesAprobacion = new DateTime(2025, 8, 1), PorcEjecFisica = null });
        db.Redeterminaciones.Add(new Redeterminacion { Id = 1, CodigoAutorizante = "B-1", Nro = 1, EtapaId = 12, UsuarioId = 9, Expediente = "EX-R" });
        db.Legitimos.AddRange(
            new Legitimo { Id = 1, ObraId = 10, CodigoAutorizante = "L-1", Certificado = 100m },
            new Legitimo { Id = 2, ObraId = 20, CodigoAutorizante = "L-2", Certificado = 5m });
        db.ExpedientesReliq.Add(new ExpedienteReliq { Id = 1, CodigoRedet = "A-1-R1", MesAprobacion = new DateTime(2025, 7, 10), Expediente = "EX-OLD" });
        await db.SaveChangesAsync();
        return new Factory(opciones);
    }

    [TestMethod]
    public async Task Autorizantes_FiltranPorArea_YCarganObraEstadoYConcepto()
    {
        var repo = new AutorizanteRepository(await SembrarAsync());

        var todos = await repo.GetAllAsync(null);
        Assert.AreEqual(4, todos.Count);
        var a1 = todos.Single(a => a.CodigoAutorizante == "A-1");
        Assert.AreEqual("OBRA 10", a1.Obra?.Descripcion);
        Assert.AreEqual("A1", a1.Obra?.Area?.Nombre);
        Assert.AreEqual("BASICO", a1.Concepto?.Nombre);
        Assert.AreEqual("EN EJECUCION", a1.Estado?.Nombre);

        CollectionAssert.AreEquivalent(new[] { "B-1", "C-1" }, (await repo.GetAllAsync([2])).Select(a => a.CodigoAutorizante).ToList());
        Assert.AreEqual(0, (await repo.GetAllAsync([])).Count, "lista vacía: nada (fail-closed)");
        CollectionAssert.AreEquivalent(new[] { "A-1", "B-1" }, (await repo.GetPorCodigosAsync(["A-1", "B-1", "ZZ"])).Select(a => a.CodigoAutorizante).ToList());
    }

    [TestMethod]
    public async Task Autorizantes_DependenciasSumaYSeguimiento()
    {
        var repo = new AutorizanteRepository(await SembrarAsync());

        Assert.IsTrue(await repo.TieneDependenciasAsync("A-1"), "tiene certificados");
        Assert.IsTrue(await repo.TieneDependenciasAsync("B-1"), "tiene redeterminaciones");
        Assert.IsFalse(await repo.TieneDependenciasAsync("C-1"));

        Assert.AreEqual(300m, await repo.SumaCertificadosAsync("A-1"));
        Assert.AreEqual(0m, await repo.SumaCertificadosAsync("C-1"));

        Assert.IsTrue(await repo.UpdateSeguimientoAsync(1, "EX-NUEVO", null));
        var a1 = await repo.GetByIdAsync(1);
        Assert.AreEqual("EX-NUEVO", a1!.Expediente);
        Assert.AreEqual(1, a1.EstadoId, "estado null: no se toca");
        Assert.IsFalse(await repo.UpdateSeguimientoAsync(999, "x", 1));
    }

    [TestMethod]
    public async Task Certificados_EjecucionFisicaPorCodigo_SoloConPorcentajeCargado()
    {
        var repo = new CertificadoRepository(await SembrarAsync());

        var ejec = await repo.GetEjecFisicaPorCodigoAsync(["A-1", "A-2", "ZZ"]);
        Assert.AreEqual(50m, ejec["A-1"]);
        Assert.IsFalse(ejec.ContainsKey("A-2"), "sin porcentaje cargado no aparece");
        Assert.AreEqual(2, (await repo.GetPorCodigosAsync(["A-1"])).Count);
        Assert.AreEqual(0, (await repo.GetPorCodigosAsync([])).Count);
    }

    [TestMethod]
    public async Task Legitimos_FiltranPorArea()
    {
        var repo = new LegitimoRepository(await SembrarAsync());
        Assert.AreEqual(2, (await repo.GetAllAsync(null)).Count);
        Assert.AreEqual("L-2", (await repo.GetAllAsync([2])).Single().CodigoAutorizante);
        Assert.AreEqual(0, (await repo.GetAllAsync([])).Count);
    }

    [TestMethod]
    public async Task Redeterminaciones_CarganEtapaYUsuario_YElSeguimientoDesasigna()
    {
        var repo = new RedeterminacionRepository(await SembrarAsync());

        var r = (await repo.GetAllAsync()).Single();
        Assert.AreEqual("RD-11/11-Notificada", r.Etapa?.Nombre);
        Assert.AreEqual("ANA", r.Usuario?.Nombre);
        Assert.AreEqual(1, (await repo.GetPorCodigosAsync(["B-1"])).Count);

        Assert.IsTrue(await repo.UpdateSeguimientoAsync(1, etapaId: null, expediente: null, usuarioId: null, borrarUsuario: false));
        var sinCambios = await repo.GetByIdAsync(1);
        Assert.AreEqual(12, sinCambios!.EtapaId);
        Assert.AreEqual("EX-R", sinCambios.Expediente);
        Assert.AreEqual(9, sinCambios.UsuarioId, "null: no se toca");

        Assert.IsTrue(await repo.UpdateSeguimientoAsync(1, etapaId: 12, expediente: "", usuarioId: null, borrarUsuario: true));
        var desasignada = await repo.GetByIdAsync(1);
        Assert.IsNull(desasignada!.UsuarioId);
        Assert.AreEqual("", desasignada.Expediente);
        Assert.IsFalse(await repo.UpdateSeguimientoAsync(999, 12, "x", null, false));
    }

    [TestMethod]
    public async Task ExpedientesReliq_SeIdentificanPorCodigoYAnioMes()
    {
        var repo = new ExpedienteReliqRepository(await SembrarAsync());

        // Mismo año y mes que la fila sembrada (día distinto): se actualiza.
        await repo.GuardarOActualizarAsync("A-1-R1", new DateTime(2025, 7, 1), "EX-NEW");
        // Otro mes: se crea; vacío: queda vacío.
        await repo.GuardarOActualizarAsync("A-1-R1", new DateTime(2025, 8, 1), null);

        var todos = (await repo.GetAllAsync()).OrderBy(e => e.MesAprobacion).ToList();
        Assert.AreEqual(2, todos.Count);
        Assert.AreEqual("EX-NEW", todos[0].Expediente);
        Assert.AreEqual(new DateTime(2025, 7, 10), todos[0].MesAprobacion, "la fecha original se conserva");
        Assert.AreEqual(string.Empty, todos[1].Expediente);
        Assert.AreEqual(new DateTime(2025, 8, 1), todos[1].MesAprobacion);
    }

    [TestMethod]
    public async Task Lookups_UsuariosDeRedeterminacionesActivos_YAutorizantesConTextoDelLegado()
    {
        var repo = new LookupRepository(await SembrarAsync());

        var usuarios = await repo.GetUsuariosRedeterminacionesAsync();
        Assert.AreEqual("ANA", usuarios.Single().Nombre, "sólo activos del área 16");

        var autorizantes = await repo.GetAutorizantesAsync([1]);
        CollectionAssert.AreEqual(new[] { "A-1 - OBRA 10 - E - D1", "A-2 - OBRA 10 - E - D2" }, autorizantes.Select(a => a.Nombre).ToList());
        Assert.AreEqual(4, (await repo.GetAutorizantesAsync(null)).Count);
        Assert.AreEqual(0, (await repo.GetAutorizantesAsync([])).Count);
        Assert.AreEqual("RD-11/11-Notificada", (await repo.GetEstadosRedetAsync()).Single().Nombre);
        Assert.AreEqual("BASICO", (await repo.GetConceptosAsync()).Single().Nombre);
    }
}
