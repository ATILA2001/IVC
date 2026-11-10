using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Obras;
using IVC.Application.Obras.Dtos;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// El servicio de obras aplica las reglas del usuario: qué obras consulta (todas o las de
/// sus áreas), en qué área puede crear, que el área no cambie al editar y que ninguna
/// escritura pase con la planificación cerrada.
/// </summary>
[TestClass]
public class ObrasServiceTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private sealed class Armado
    {
        public Mock<IObraRepository> Repo { get; } = new();
        public Mock<ILookupRepository> Lookups { get; } = new();
        public Mock<IUsuarioActual> UsuarioActual { get; } = new();
        public Mock<IPermisosIvc> Permisos { get; } = new();

        public Armado(UsuarioIvc usuario, bool planificacionAbierta = true)
        {
            UsuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
            Permisos.Setup(p => p.PuedeModificarPlanificacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(planificacionAbierta);
            Repo.Setup(r => r.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([ObraDePrueba()]);
            Repo.Setup(r => r.GetAgregadosAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, AgregadosObra>
                {
                    [10] = new(1000m, 600m, 100m, null, null, 300m, null, null, [new(1000m, 50m)]),
                });
            Lookups.Setup(l => l.GetAreasAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new OpcionLookup(1, "OBRAS"), new OpcionLookup(26, "DGDH"), new OpcionLookup(40, "FINANCIERA")]);
        }

        public ObrasService Servicio() => new(Repo.Object, Lookups.Object, UsuarioActual.Object, Permisos.Object, NullLogger<ObrasService>.Instance);
    }

    private static Obra ObraDePrueba() => new()
    {
        Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024, Etapa = 1, ObraNumero = 7,
        AreaId = 26, Area = new Area { Id = 26, Nombre = "DGDH" },
        EmpresaId = 2, Empresa = new Empresa { Id = 2, Nombre = "CONSTRUCTORA" },
        ContrataId = 3, Contrata = new Contrata { Id = 3, Nombre = "LP" },
        BarrioId = 4, Barrio = new Barrio { Id = 4, Nombre = "LUGANO" },
        Proyecto = new Proyecto { Nombre = "PROY", Autorizado2026 = 2000m, LineaGestion = new LineaGestion { Nombre = "VIVIENDA" } },
    };

    [TestMethod]
    public async Task GetAll_AdminYRedeterminaciones_PidenTodasLasObras()
    {
        foreach (var usuario in new[] { Usuario(admin: true), Usuario(admin: false, IvcAreaIds.Redeterminaciones) })
        {
            var armado = new Armado(usuario);
            await armado.Servicio().GetAllAsync();
            armado.Repo.Verify(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);
            armado.Repo.Verify(r => r.GetAgregadosAsync(null, It.IsAny<CancellationToken>()), Times.Once, "sin filtro, los agregados también son de todo");
        }
    }

    [TestMethod]
    public async Task GetAll_UsuarioNormal_PideSoloSusAreas_YSinAreasNoConsulta()
    {
        var conAreas = new Armado(Usuario(admin: false, 26, 40));
        await conAreas.Servicio().GetAllAsync();
        conAreas.Repo.Verify(r => r.GetAllAsync(
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 26, 40 })), It.IsAny<CancellationToken>()), Times.Once);
        conAreas.Repo.Verify(r => r.GetAgregadosAsync(
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 10 })), It.IsAny<CancellationToken>()), Times.Once,
            "los agregados se piden sólo para las obras que se van a mostrar");

        var sinAreas = new Armado(Usuario(admin: false));
        Assert.AreEqual(0, (await sinAreas.Servicio().GetAllAsync()).Count);
        sinAreas.Repo.Verify(r => r.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()), Times.Never,
            "sin áreas resueltas no ve nada: fail-closed");
    }

    [TestMethod]
    public async Task GetAll_MapeaLaObraYSusIndicadores()
    {
        var fila = (await new Armado(Usuario(admin: true)).Servicio().GetAllAsync()).Single();

        Assert.AreEqual("TORRE A", fila.Descripcion);
        Assert.AreEqual("LP 12/2024", fila.ContrataTexto);
        Assert.AreEqual("VIVIENDA", fila.LineaGestionNombre);
        Assert.AreEqual(2000m, fila.Autorizado2026, "del proyecto de la obra");
        Assert.AreEqual(400m, fila.MontoCertificado);
        Assert.AreEqual(20m, fila.Porcentaje);
        Assert.AreEqual(50m, fila.EjecFisica);
    }

    [TestMethod]
    public async Task Create_ConPlanificacionCerrada_SeRechaza_YUsuarioNormal_SoloEnSuArea()
    {
        var vm = new ObraViewModel
        {
            Descripcion = "NUEVA", AreaId = 40, EmpresaId = 2, ContrataId = 3, BarrioId = 4, Numero = 1, Anio = 2026, Etapa = 1, ObraNumero = 1,
        };

        var cerrada = new Armado(Usuario(admin: true), planificacionAbierta: false);
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => cerrada.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex.Message, "planificación está cerrada");

        var otraArea = new Armado(Usuario(admin: false, 26));
        var ex2 = await Assert.ThrowsExceptionAsync<ArgumentException>(() => otraArea.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex2.Message, "propia área");
        otraArea.Repo.Verify(r => r.AddAsync(It.IsAny<Obra>(), It.IsAny<CancellationToken>()), Times.Never);

        var propia = new Armado(Usuario(admin: false, 40));
        propia.Repo.Setup(r => r.AddAsync(It.IsAny<Obra>(), It.IsAny<CancellationToken>()))
            .Callback<Obra, CancellationToken>((o, _) => o.Id = 99).Returns(Task.CompletedTask);
        propia.Repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(ObraDePrueba());
        await propia.Servicio().CreateAsync(vm);
        propia.Repo.Verify(r => r.AddAsync(It.Is<Obra>(o => o.AreaId == 40 && o.Descripcion == "NUEVA" && o.ContrataId == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Update_ConservaElArea_YNoEnviaNavegaciones_YLaFilaBorradaEsConflicto()
    {
        var armado = new Armado(Usuario(admin: true));
        armado.Repo.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(ObraDePrueba());
        armado.Repo.Setup(r => r.GetByIdAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync((Obra?)null);
        Obra? guardada = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Obra>(), It.IsAny<CancellationToken>()))
            .Callback<Obra, CancellationToken>((o, _) => guardada = o).Returns(Task.CompletedTask);
        var servicio = armado.Servicio();

        await servicio.UpdateAsync(new ObraViewModel
        {
            Id = 10, AreaId = 999, Descripcion = " TORRE A BIS ", EmpresaId = 5, ContrataId = 3, BarrioId = 4, Numero = 13, Anio = 2025, Etapa = 2, ObraNumero = 8,
        });

        Assert.IsNotNull(guardada);
        Assert.AreEqual(26, guardada!.AreaId, "el área no se cambia desde la edición");
        Assert.AreEqual("TORRE A BIS", guardada.Descripcion);
        Assert.AreEqual(5, guardada.EmpresaId);
        Assert.IsNull(guardada.Empresa);
        Assert.IsNull(guardada.Proyecto);

        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => servicio.UpdateAsync(new ObraViewModel
        {
            Id = 77, AreaId = 1, Descripcion = "X", EmpresaId = 5, ContrataId = 3, BarrioId = 4, Numero = 1, Anio = 2025, Etapa = 1, ObraNumero = 1,
        }));
    }

    [TestMethod]
    public async Task Delete_EnUso_EsMensaje_YAreasPermitidas_SegunElUsuario()
    {
        var armado = new Armado(Usuario(admin: false, 26));
        armado.Repo.Setup(r => r.DeleteAsync(10, It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("FK"));
        var servicio = armado.Servicio();

        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.DeleteAsync(10));
        StringAssert.Contains(ex.Message, "autorizantes");

        var areas = await servicio.GetAreasPermitidasAsync();
        Assert.AreEqual("DGDH", areas.Single().Nombre, "el usuario normal sólo da de alta en sus áreas");
        Assert.AreEqual(3, (await new Armado(Usuario(admin: true)).Servicio().GetAreasPermitidasAsync()).Count);
    }
}
