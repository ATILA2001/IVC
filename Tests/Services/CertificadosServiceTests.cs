using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// El servicio de certificados: filtro por área (sólo el administrador ve todo), las
/// reliquidaciones virtuales que generan las redeterminaciones, el SIGAF prorrateado entre
/// los pagos del mismo expediente, y las escrituras (alta en autorizantes del área, edición
/// en ventana sin tocar el autorizante, inline en reales y en reliquidaciones).
/// </summary>
[TestClass]
public class CertificadosServiceTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private sealed class Armado
    {
        public Mock<ICertificadoRepository> Repo { get; } = new();
        public Mock<IAutorizanteRepository> Autorizantes { get; } = new();
        public Mock<IRedeterminacionRepository> Redets { get; } = new();
        public Mock<IExpedienteReliqRepository> ExpReliq { get; } = new();
        public Mock<IDatosExternosService> Externos { get; } = new();
        public Mock<IUsuarioActual> UsuarioActual { get; } = new();
        public Mock<IPermisosIvc> Permisos { get; } = new();

        public Armado(UsuarioIvc usuario, bool planificacionAbierta = true, bool inline = true)
        {
            UsuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
            Permisos.Setup(p => p.PuedeModificarPlanificacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(planificacionAbierta);
            Permisos.Setup(p => p.PuedeEditarInlineAsync(It.IsAny<CancellationToken>())).ReturnsAsync(inline);
            Autorizantes.Setup(a => a.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([AutorizanteDePrueba()]);
            Autorizantes.Setup(a => a.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(AutorizanteDePrueba());
            Repo.Setup(r => r.GetPorCodigosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([CertificadoPreSalto(), CertificadoPostSalto()]);
            Repo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(CertificadoPostSalto());
            Redets.Setup(r => r.GetPorCodigosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Redeterminacion
                {
                    Id = 5, CodigoAutorizante = "OBRA-1", CodigoRedet = "OBRA-1-R1", Nro = 1, Porcentaje = 10m, Salto = new DateTime(2025, 6, 1), EtapaId = 12,
                }]);
            ExpReliq.Setup(e => e.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ExpedienteReliq { Id = 1, CodigoRedet = "OBRA-1-R1", MesAprobacion = new DateTime(2025, 7, 15), Expediente = "EX-B" }]);
            Externos.Setup(e => e.GetDevengadosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, decimal> { ["EX-A"] = 150m, ["EX-B"] = 170m });
            Externos.Setup(e => e.GetUltimosPasesSadeAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, UltimoPaseSade> { ["EX-B"] = new("IVC-4120 DEPTO PRESUPUESTOS", new DateTime(2026, 10, 1)) });
        }

        public CertificadosService Servicio() => new(
            Repo.Object, Autorizantes.Object, Redets.Object, ExpReliq.Object, Externos.Object, UsuarioActual.Object, Permisos.Object,
            NullLogger<CertificadosService>.Instance);
    }

    private static Autorizante AutorizanteDePrueba() => new()
    {
        Id = 1, CodigoAutorizante = "OBRA-1", MontoAutorizado = 1000m, ObraId = 10, EstadoId = 1,
        Obra = new Obra
        {
            Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024, AreaId = 26, Area = new Area { Id = 26, Nombre = "DGDH" },
            EmpresaId = 2, Empresa = new Empresa { Id = 2, Nombre = "CONSTRUCTORA" }, Contrata = new Contrata { Id = 3, Nombre = "LP" },
            Barrio = new Barrio { Id = 4, Nombre = "LUGANO" },
            Proyecto = new Proyecto { Nombre = "PROY", LineaGestion = new LineaGestion { Nombre = "VIVIENDA" } },
        },
        Estado = new EstadoAutorizante { Id = 1, Nombre = "EN EJECUCION" },
    };

    private static Certificado CertificadoPreSalto() => new()
    {
        Id = 1, CodigoAutorizante = "OBRA-1", MontoTotal = 200m, MesAprobacion = new DateTime(2025, 3, 1), ExpedientePago = "EX-A ", // espacio final a propósito
        TipoPagoId = 2, TipoPago = new TipoPago { Id = 2, Nombre = "CERTIFICADO" }, PorcEjecFisica = 10m,
    };

    private static Certificado CertificadoPostSalto() => new()
    {
        Id = 2, CodigoAutorizante = "OBRA-1", MontoTotal = 300m, MesAprobacion = new DateTime(2025, 7, 1), ExpedientePago = "EX-B",
        TipoPagoId = 2, TipoPago = new TipoPago { Id = 2, Nombre = "CERTIFICADO" }, PorcEjecFisica = 15m,
    };

    [TestMethod]
    public async Task GetAll_SoloElAdminVeTodo_RedeterminacionesTambienFiltraPorSusAreas()
    {
        var admin = new Armado(Usuario(admin: true));
        await admin.Servicio().GetAllAsync();
        admin.Autorizantes.Verify(a => a.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);

        var redet = new Armado(Usuario(admin: false, IvcAreaIds.Redeterminaciones));
        await redet.Servicio().GetAllAsync();
        redet.Autorizantes.Verify(a => a.GetAllAsync(
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { IvcAreaIds.Redeterminaciones })), It.IsAny<CancellationToken>()),
            Times.Once, "en Certificados el legado filtraba a todo no-admin por sus áreas");

        var sinAreas = new Armado(Usuario(admin: false));
        Assert.AreEqual(0, (await sinAreas.Servicio().GetAllAsync()).Count);
        sinAreas.Autorizantes.Verify(a => a.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetAll_GeneraLaReliquidacion_YProrrateaElSigafEntreLosPagosDelMismoExpediente()
    {
        var filas = await new Armado(Usuario(admin: true)).Servicio().GetAllAsync();

        Assert.AreEqual(3, filas.Count, "dos certificados reales y una reliquidación");
        var pre = filas.Single(f => f.Id == 1);
        Assert.AreEqual("LP 12/2024", pre.Contrata);
        Assert.AreEqual("VIVIENDA", pre.LineaGestion);
        Assert.AreEqual("PROY", pre.Proyecto);
        Assert.AreEqual("EN EJECUCION", pre.AutorizanteEstado);
        Assert.AreEqual("CERTIFICADO", pre.TipoPago);
        Assert.AreEqual(20m, pre.Porcentaje, "200 sobre 1000");
        Assert.AreEqual(150m, pre.Sigaf, "único pago del expediente: se lleva todo el devengado (y el espacio final no lo esconde)");
        Assert.AreEqual("DEVENGADO", pre.Estado);
        Assert.IsNull(pre.BuzonSade, "EX-A no tiene pases");

        var reliq = filas.Single(f => f.EsReliquidacion);
        Assert.AreEqual("OBRA-1-R1", reliq.CodigoAutorizante);
        Assert.AreEqual(1, reliq.AutorizanteId);
        Assert.AreEqual("TORRE A", reliq.Obra, "datos de obra del autorizante de la redeterminación");
        Assert.AreEqual("APROBADO", reliq.AutorizanteEstado);
        Assert.AreEqual("RELIQUIDACION", reliq.TipoPago);
        Assert.AreEqual(new DateTime(2025, 7, 1), reliq.MesAprobacion, "el mes del certificado afectado");
        Assert.AreEqual("EX-B", reliq.ExpedientePago, "de EXPEDIENTES_RELIQ, por código y año/mes");
        // MontoRedet = 1000 * 10 % * (1 - 20 %) = 80; el primer certificado post-salto (30 %) absorbe el 20 % pre-salto: 50 % de 80 = 40.
        Assert.AreEqual(50m, reliq.Porcentaje);
        Assert.AreEqual(40m, reliq.MontoTotal);
        Assert.AreEqual("OBRA-1-R1|202507", reliq.ClaveReliquidacion);

        var post = filas.Single(f => f.Id == 2);
        // EX-B tiene dos pagos (300 y 40) y 170 devengados: 170 * 300 / 340 = 150 y 170 * 40 / 340 = 20.
        Assert.AreEqual(150m, post.Sigaf);
        Assert.AreEqual(20m, reliq.Sigaf);
        Assert.AreEqual("DEVENGADO", reliq.Estado);
        Assert.AreEqual("IVC-4120 DEPTO PRESUPUESTOS", reliq.BuzonSade);
        Assert.AreEqual(new DateTime(2026, 10, 1), post.FechaSade);
    }

    [TestMethod]
    public async Task Create_ExigeAutorizanteDelArea_YUsaSuCodigo()
    {
        var vm = new CertificadoViewModel { AutorizanteId = 1, TipoPagoId = 2, MontoTotal = 50m, MesAprobacion = new DateTime(2026, 1, 1), ExpedientePago = " EX-N " };

        var otraArea = new Armado(Usuario(admin: false, 40));
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => otraArea.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex.Message, "propia área");

        var propia = new Armado(Usuario(admin: false, 26));
        propia.Repo.Setup(r => r.AddAsync(It.IsAny<Certificado>(), It.IsAny<CancellationToken>()))
            .Callback<Certificado, CancellationToken>((c, _) => c.Id = 9).Returns(Task.CompletedTask);
        var creado = await propia.Servicio().CreateAsync(vm);
        propia.Repo.Verify(r => r.AddAsync(It.Is<Certificado>(c => c.CodigoAutorizante == "OBRA-1" && c.ExpedientePago == "EX-N" && c.MontoTotal == 50m), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(9, creado.Id);
        Assert.AreEqual("TORRE A", creado.Obra);
        Assert.AreEqual("EN TRAMITE", creado.Estado, "con expediente y sin devengado");

        var cerrada = new Armado(Usuario(admin: true), planificacionAbierta: false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => cerrada.Servicio().CreateAsync(vm));
    }

    [TestMethod]
    public async Task Update_NoTocaReliquidaciones_YConservaCodigoYEjecucionFisica()
    {
        var armado = new Armado(Usuario(admin: true));
        Certificado? guardado = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Certificado>(), It.IsAny<CancellationToken>()))
            .Callback<Certificado, CancellationToken>((c, _) => guardado = c).Returns(Task.CompletedTask);
        var servicio = armado.Servicio();

        var reliq = new CertificadoViewModel { Id = 0, TipoPagoId = ReglasRedeterminaciones.TipoPagoReliquidacionId, MontoTotal = 1m, MesAprobacion = new DateTime(2025, 7, 1) };
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.UpdateAsync(reliq));
        StringAssert.Contains(ex.Message, "no se editan");

        var vm = new CertificadoViewModel { Id = 2, AutorizanteId = 999, TipoPagoId = 1, MontoTotal = 350m, MesAprobacion = new DateTime(2025, 8, 1), ExpedientePago = "EX-C", PorcEjecFisica = 99m };
        await servicio.UpdateAsync(vm);
        Assert.IsNotNull(guardado);
        Assert.AreEqual("OBRA-1", guardado.CodigoAutorizante, "el autorizante no se cambia desde la ventana");
        Assert.AreEqual(15m, guardado.PorcEjecFisica, "la ejecución física sólo se edita inline");
        Assert.AreEqual(1, guardado.TipoPagoId);
        Assert.AreEqual(350m, guardado.MontoTotal);
        Assert.AreEqual(new DateTime(2025, 8, 1), guardado.MesAprobacion);

        vm.Id = 77;
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => servicio.UpdateAsync(vm));
    }

    [TestMethod]
    public async Task Inline_LaReliquidacionGuardaEnExpedientesReliq_YElRealSoloExpedienteMontoYEjecucion()
    {
        var armado = new Armado(Usuario(admin: false, 26), planificacionAbierta: false);
        Certificado? guardado = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Certificado>(), It.IsAny<CancellationToken>()))
            .Callback<Certificado, CancellationToken>((c, _) => guardado = c).Returns(Task.CompletedTask);
        var servicio = armado.Servicio();

        var reliq = new CertificadoViewModel
        {
            Id = 0, TipoPagoId = ReglasRedeterminaciones.TipoPagoReliquidacionId, CodigoAutorizante = "OBRA-1-R1", MesAprobacion = new DateTime(2025, 7, 1), ExpedientePago = " EX-Z ",
        };
        await servicio.ActualizarInlineAsync(reliq);
        armado.ExpReliq.Verify(e => e.GuardarOActualizarAsync("OBRA-1-R1", new DateTime(2025, 7, 1), "EX-Z", It.IsAny<CancellationToken>()), Times.Once);
        armado.Repo.Verify(r => r.UpdateAsync(It.IsAny<Certificado>(), It.IsAny<CancellationToken>()), Times.Never);

        var real = new CertificadoViewModel { Id = 2, TipoPagoId = 7, MesAprobacion = new DateTime(2030, 1, 1), ExpedientePago = "EX-D", MontoTotal = 310m, PorcEjecFisica = 40m };
        await servicio.ActualizarInlineAsync(real);
        Assert.IsNotNull(guardado);
        Assert.AreEqual("EX-D", guardado.ExpedientePago);
        Assert.AreEqual(310m, guardado.MontoTotal);
        Assert.AreEqual(40m, guardado.PorcEjecFisica);
        Assert.AreEqual(2, guardado.TipoPagoId, "mes y tipo se conservan del guardado");
        Assert.AreEqual(new DateTime(2025, 7, 1), guardado.MesAprobacion);

        real.MontoTotal = -1m;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.ActualizarInlineAsync(real));

        var sinPermiso = new Armado(Usuario(admin: false, 26), inline: false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => sinPermiso.Servicio().ActualizarInlineAsync(reliq));
    }
}
