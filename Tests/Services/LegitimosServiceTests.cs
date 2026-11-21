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
/// El servicio de legítimos: filtro por área, SIGAF total del expediente (sin prorrateo,
/// R4), estado y SADE por fila, y las escrituras con la obra fija y el inline acotado.
/// </summary>
[TestClass]
public class LegitimosServiceTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private sealed class Armado
    {
        public Mock<ILegitimoRepository> Repo { get; } = new();
        public Mock<IObraRepository> Obras { get; } = new();
        public Mock<IDatosExternosService> Externos { get; } = new();
        public Mock<IUsuarioActual> UsuarioActual { get; } = new();
        public Mock<IPermisosIvc> Permisos { get; } = new();

        public Armado(UsuarioIvc usuario, bool planificacionAbierta = true, bool inline = true)
        {
            UsuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
            Permisos.Setup(p => p.PuedeModificarPlanificacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(planificacionAbierta);
            Permisos.Setup(p => p.PuedeEditarInlineAsync(It.IsAny<CancellationToken>())).ReturnsAsync(inline);
            Repo.Setup(r => r.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([LegitimoDePrueba(), new Legitimo { Id = 2, ObraId = 10, CodigoAutorizante = "OBRA-9", Expediente = null, Certificado = 5m }]);
            Repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(LegitimoDePrueba());
            Externos.Setup(e => e.GetDevengadosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, decimal> { ["EX-L"] = 123m });
            Externos.Setup(e => e.GetUltimosPasesSadeAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, UltimoPaseSade> { ["EX-L"] = new("IVC-4100 GO ECONOMICA FINANCIERA", new DateTime(2026, 9, 1)) });
            Obras.Setup(o => o.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new Obra { Id = 10, AreaId = 26, EmpresaId = 2 });
        }

        public LegitimosService Servicio() => new(Repo.Object, Obras.Object, Externos.Object, UsuarioActual.Object, Permisos.Object, NullLogger<LegitimosService>.Instance);
    }

    private static Legitimo LegitimoDePrueba() => new()
    {
        Id = 1, ObraId = 10, CodigoAutorizante = "OBRA-1", Expediente = "EX-L", Certificado = 500m,
        InicioEjecucion = new DateTime(2025, 1, 1), FinEjecucion = new DateTime(2025, 3, 1), MesAprobacion = new DateTime(2025, 4, 1), PorcEjecFisica = 12m,
        Obra = new Obra
        {
            Id = 10, Descripcion = "TORRE A", AreaId = 26, Area = new Area { Id = 26, Nombre = "DGDH" }, EmpresaId = 2,
            Empresa = new Empresa { Id = 2, Nombre = "CONSTRUCTORA" }, Barrio = new Barrio { Id = 4, Nombre = "LUGANO" },
            Proyecto = new Proyecto { Nombre = "PROY", LineaGestion = new LineaGestion { Nombre = "VIVIENDA" } },
        },
    };

    [TestMethod]
    public async Task GetAll_FiltraPorArea_YCompletaSigafTotalEstadoYSade()
    {
        var admin = new Armado(Usuario(admin: true));
        var filas = await admin.Servicio().GetAllAsync();
        admin.Repo.Verify(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);

        var conExpediente = filas.Single(f => f.Id == 1);
        Assert.AreEqual("DGDH", conExpediente.Area);
        Assert.AreEqual("VIVIENDA", conExpediente.LineaGestion);
        Assert.AreEqual(123m, conExpediente.Sigaf, "el devengado completo, sin prorrateo (R4)");
        Assert.AreEqual("DEVENGADO", conExpediente.Estado);
        Assert.AreEqual("IVC-4100 GO ECONOMICA FINANCIERA", conExpediente.BuzonSade);
        var sinExpediente = filas.Single(f => f.Id == 2);
        Assert.AreEqual("NO INICIADO", sinExpediente.Estado);
        Assert.IsNull(sinExpediente.Sigaf);

        var normal = new Armado(Usuario(admin: false, 26));
        await normal.Servicio().GetAllAsync();
        normal.Repo.Verify(r => r.GetAllAsync(
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 26 })), It.IsAny<CancellationToken>()), Times.Once);

        var sinAreas = new Armado(Usuario(admin: false));
        Assert.AreEqual(0, (await sinAreas.Servicio().GetAllAsync()).Count);
        sinAreas.Repo.Verify(r => r.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Create_ExigeObraDelArea_YUpdate_ConservaLaObra()
    {
        var vm = new LegitimoViewModel
        {
            ObraId = 10, CodigoAutorizante = " OBRA-1 ", Expediente = "EX-N", InicioEjecucion = new DateTime(2026, 1, 1), FinEjecucion = new DateTime(2026, 2, 1),
            Certificado = 10m, MesAprobacion = new DateTime(2026, 2, 1),
        };
        var otraArea = new Armado(Usuario(admin: false, 40));
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => otraArea.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex.Message, "propia área");

        var armado = new Armado(Usuario(admin: false, 26));
        armado.Repo.Setup(r => r.AddAsync(It.IsAny<Legitimo>(), It.IsAny<CancellationToken>()))
            .Callback<Legitimo, CancellationToken>((l, _) => l.Id = 1).Returns(Task.CompletedTask);
        var creado = await armado.Servicio().CreateAsync(vm);
        armado.Repo.Verify(r => r.AddAsync(It.Is<Legitimo>(l => l.ObraId == 10 && l.CodigoAutorizante == "OBRA-1"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual("TORRE A", creado.Obra, "vuelve releído");

        Legitimo? guardado = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Legitimo>(), It.IsAny<CancellationToken>()))
            .Callback<Legitimo, CancellationToken>((l, _) => guardado = l).Returns(Task.CompletedTask);
        vm.Id = 1;
        vm.ObraId = 99;
        await armado.Servicio().UpdateAsync(vm);
        Assert.IsNotNull(guardado);
        Assert.AreEqual(10, guardado.ObraId, "la obra no se cambia desde la edición");
        Assert.AreEqual(12m, guardado.PorcEjecFisica, "columna que la ventana no toca");
        Assert.IsNull(guardado.Obra, "sin navegaciones");

        vm.Id = 77;
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => armado.Servicio().UpdateAsync(vm));
    }

    [TestMethod]
    public async Task Inline_SoloExpedienteYMonto_SinDependerDeLaPlanificacion()
    {
        var armado = new Armado(Usuario(admin: false, 26), planificacionAbierta: false);
        Legitimo? guardado = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Legitimo>(), It.IsAny<CancellationToken>()))
            .Callback<Legitimo, CancellationToken>((l, _) => guardado = l).Returns(Task.CompletedTask);

        await armado.Servicio().ActualizarInlineAsync(new LegitimoViewModel { Id = 1, Expediente = "  ", Certificado = 600m, CodigoAutorizante = "OTRO", MesAprobacion = new DateTime(2030, 1, 1) });
        Assert.IsNotNull(guardado);
        Assert.AreEqual(string.Empty, guardado.Expediente, "vacío se guarda como cadena vacía, como el legado");
        Assert.AreEqual(600m, guardado.Certificado);
        Assert.AreEqual("OBRA-1", guardado.CodigoAutorizante, "lo demás se conserva del guardado");
        Assert.AreEqual(new DateTime(2025, 4, 1), guardado.MesAprobacion);

        var sinPermiso = new Armado(Usuario(admin: false, 26), inline: false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => sinPermiso.Servicio().ActualizarInlineAsync(new LegitimoViewModel { Id = 1, Certificado = 1m }));

        var cerrada = new Armado(Usuario(admin: false, 26), planificacionAbierta: false);
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => cerrada.Servicio().DeleteAsync(1));
        StringAssert.Contains(ex.Message, "planificación está cerrada");
    }
}
