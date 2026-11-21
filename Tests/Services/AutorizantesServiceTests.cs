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
/// El servicio de autorizantes: qué autorizantes consulta cada usuario, cómo arma las filas
/// virtuales de las redeterminaciones (monto calculado y estado general), y las reglas de
/// alta (obra propia, sin código, AUTORIZACION_GG), edición (monto no menor a lo
/// certificado), baja (sin dependencias) y seguimiento inline.
/// </summary>
[TestClass]
public class AutorizantesServiceTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private sealed class Armado
    {
        public Mock<IAutorizanteRepository> Repo { get; } = new();
        public Mock<IRedeterminacionRepository> Redets { get; } = new();
        public Mock<ICertificadoRepository> Certs { get; } = new();
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
                .ReturnsAsync([AutorizanteDePrueba()]);
            Repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(AutorizanteDePrueba());
            Redets.Setup(r => r.GetPorCodigosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([RedeterminacionDePrueba()]);
            Certs.Setup(c => c.GetPorCodigosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new Certificado { Id = 1, CodigoAutorizante = "OBRA-1", MontoTotal = 200m, MesAprobacion = new DateTime(2025, 3, 1) },
                    new Certificado { Id = 2, CodigoAutorizante = "OBRA-1", MontoTotal = 300m, MesAprobacion = new DateTime(2025, 7, 1) },
                ]);
            Certs.Setup(c => c.GetEjecFisicaPorCodigoAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, decimal> { ["OBRA-1"] = 45m });
            Externos.Setup(e => e.GetUltimosPasesSadeAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, UltimoPaseSade> { ["EX-1"] = new("IVC-4010 DEPTO REDETERMINACIONES", new DateTime(2026, 11, 1)) });
            Externos.Setup(e => e.GetAcdirsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string> { ["EX-R1"] = "ACDIR-99" });
            Obras.Setup(o => o.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new Obra { Id = 10, AreaId = 26, EmpresaId = 2 });
        }

        public AutorizantesService Servicio() => new(
            Repo.Object, Redets.Object, Certs.Object, Obras.Object, Externos.Object, UsuarioActual.Object, Permisos.Object,
            NullLogger<AutorizantesService>.Instance);
    }

    private static Autorizante AutorizanteDePrueba() => new()
    {
        Id = 1, CodigoAutorizante = "OBRA-1", Detalle = "BASICO", Expediente = "EX-1", MontoAutorizado = 1000m,
        MesAprobacion = new DateTime(2025, 1, 1), MesBase = new DateTime(2024, 12, 1), AutorizacionGG = true,
        ObraId = 10, ConceptoId = 4, EstadoId = 1,
        Obra = new Obra
        {
            Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024, AreaId = 26, Area = new Area { Id = 26, Nombre = "DGDH" },
            EmpresaId = 2, Empresa = new Empresa { Id = 2, Nombre = "CONSTRUCTORA" }, Contrata = new Contrata { Id = 3, Nombre = "LP" },
            Barrio = new Barrio { Id = 4, Nombre = "LUGANO" },
        },
        Concepto = new Concepto { Id = 4, Nombre = "BASICO" },
        Estado = new EstadoAutorizante { Id = 1, Nombre = "EN EJECUCION" },
    };

    private static Redeterminacion RedeterminacionDePrueba() => new()
    {
        Id = 5, CodigoAutorizante = "OBRA-1", CodigoRedet = "OBRA-1-R1", Nro = 1, Porcentaje = 10m, Salto = new DateTime(2025, 6, 1),
        Tipo = "DEFINITIVA", Expediente = "EX-R1", EtapaId = 12, Etapa = new EstadoRedet { Id = 12, Nombre = "RD-11/11-Notificada" },
    };

    [TestMethod]
    public async Task GetAll_AdminYRedeterminaciones_VenTodo_ElRestoSusAreas_YSinAreasNada()
    {
        foreach (var usuario in new[] { Usuario(admin: true), Usuario(admin: false, IvcAreaIds.Redeterminaciones) })
        {
            var armado = new Armado(usuario);
            await armado.Servicio().GetAllAsync();
            armado.Repo.Verify(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        }

        var conAreas = new Armado(Usuario(admin: false, 26, 40));
        await conAreas.Servicio().GetAllAsync();
        conAreas.Repo.Verify(r => r.GetAllAsync(
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 26, 40 })), It.IsAny<CancellationToken>()), Times.Once);

        var sinAreas = new Armado(Usuario(admin: false));
        Assert.AreEqual(0, (await sinAreas.Servicio().GetAllAsync()).Count);
        sinAreas.Repo.Verify(r => r.GetAllAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetAll_MezclaAutorizantesReales_ConLasFilasVirtualesDeSusRedeterminaciones()
    {
        var armado = new Armado(Usuario(admin: true));
        var filas = await armado.Servicio().GetAllAsync();

        Assert.AreEqual(2, filas.Count);
        var real = filas.Single(f => !f.EsRedeterminacion);
        Assert.AreEqual("OBRA-1", real.CodigoAutorizante);
        Assert.AreEqual("LP 12/2024", real.Contrata);
        Assert.AreEqual("DGDH", real.Area);
        Assert.AreEqual("BASICO", real.Concepto);
        Assert.AreEqual("EN EJECUCION", real.Estado);
        Assert.AreEqual(45m, real.EjecFisica);
        Assert.AreEqual(55m, real.FaltanteEjecFisica);
        Assert.AreEqual("IVC-4010 DEPTO REDETERMINACIONES", real.BuzonSade);
        Assert.AreEqual(new DateTime(2026, 11, 1), real.FechaSade);
        Assert.IsNull(real.Acdir);

        var virtual_ = filas.Single(f => f.EsRedeterminacion);
        Assert.AreEqual(5, virtual_.RedeterminacionId);
        Assert.AreEqual(0, virtual_.Id, "la fila virtual no lleva el id del autorizante");
        Assert.AreEqual("OBRA-1-R1", virtual_.CodigoAutorizante);
        Assert.AreEqual("TORRE A", virtual_.Obra, "los datos de obra son los del autorizante");
        Assert.AreEqual("DEFINITIVA - RD-11/11-Notificada", virtual_.Detalle);
        Assert.AreEqual(AutorizantesService.ConceptoRedeterminacionId, virtual_.ConceptoId);
        Assert.AreEqual("REDETERMINACION", virtual_.Concepto);
        Assert.AreEqual("APROBADO", virtual_.Estado, "etapa 12 => estado general aprobado");
        Assert.AreEqual(EstadoGeneral.Aprobado.Id, virtual_.EstadoId);
        // R1: 200 ejecutados antes del salto sobre 1000 => 20 % usado, 80 % faltante; 1000 * 10 % * 80 % = 80.
        Assert.AreEqual(80m, virtual_.MontoAutorizado);
        Assert.AreEqual(new DateTime(2025, 6, 1), virtual_.MesAprobacion, "el salto va en la columna de mes");
        Assert.IsNull(virtual_.MesBase);
        Assert.AreEqual("ACDIR-99", virtual_.Acdir);
        Assert.IsNull(virtual_.EjecFisica, "las redeterminaciones no tienen certificados propios");

        // Los certificados sólo se piden para los autorizantes con redeterminaciones.
        armado.Certs.Verify(c => c.GetPorCodigosAsync(
            It.Is<IReadOnlyCollection<string>>(cs => cs.SequenceEqual(new[] { "OBRA-1" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Create_ExigePlanificacionAbierta_YObraDelArea_YNoEnviaCodigo()
    {
        var vm = new AutorizanteViewModel
        {
            ObraId = 10, ConceptoId = 4, EstadoId = 1, Detalle = " NUEVO ", Expediente = "EX-9 ", MontoAutorizado = 500m, MesAprobacion = new DateTime(2026, 1, 1),
        };

        var cerrada = new Armado(Usuario(admin: true), planificacionAbierta: false);
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => cerrada.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex.Message, "planificación está cerrada");

        var otraArea = new Armado(Usuario(admin: false, 40));
        var ex2 = await Assert.ThrowsExceptionAsync<ArgumentException>(() => otraArea.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex2.Message, "propia área");
        otraArea.Repo.Verify(r => r.AddAsync(It.IsAny<Autorizante>(), It.IsAny<CancellationToken>()), Times.Never);

        var propia = new Armado(Usuario(admin: false, 26));
        // El repositorio simula al trigger: asigna el código al insertar. Se guarda lo que llegó antes.
        string? codigoEnviado = "no llamado";
        propia.Repo.Setup(r => r.AddAsync(It.IsAny<Autorizante>(), It.IsAny<CancellationToken>()))
            .Callback<Autorizante, CancellationToken>((a, _) => { codigoEnviado = a.CodigoAutorizante; a.Id = 7; a.CodigoAutorizante = "OBRA-2"; })
            .Returns(Task.CompletedTask);
        propia.Repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            var a = AutorizanteDePrueba();
            a.Id = 7;
            a.CodigoAutorizante = "OBRA-2";
            a.Detalle = "NUEVO";
            return a;
        });
        var creado = await propia.Servicio().CreateAsync(vm);
        Assert.IsNull(codigoEnviado, "el código no se envía: lo arma el trigger");
        propia.Repo.Verify(r => r.AddAsync(It.Is<Autorizante>(a =>
            a.AutorizacionGG && a.ObraId == 10 && a.Detalle == "NUEVO" && a.Expediente == "EX-9"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(7, creado.Id);
        Assert.AreEqual("OBRA-2", creado.CodigoAutorizante, "vuelve releído con el código que asignó el trigger");
        Assert.AreEqual("DGDH", creado.Area);
    }

    [TestMethod]
    public async Task Update_RechazaMontoMenorALoCertificado_ConservaObraYCodigo_YLaFilaBorradaEsConflicto()
    {
        var armado = new Armado(Usuario(admin: true));
        armado.Repo.Setup(r => r.SumaCertificadosAsync("OBRA-1", It.IsAny<CancellationToken>())).ReturnsAsync(500m);
        Autorizante? guardado = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Autorizante>(), It.IsAny<CancellationToken>()))
            .Callback<Autorizante, CancellationToken>((a, _) => guardado = a).Returns(Task.CompletedTask);
        var servicio = armado.Servicio();

        var vm = new AutorizanteViewModel
        {
            Id = 1, ObraId = 99, ConceptoId = 7, EstadoId = 2, Detalle = "EDITADO", MontoAutorizado = 400m, MesAprobacion = new DateTime(2025, 1, 1),
        };
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.UpdateAsync(vm));
        StringAssert.Contains(ex.Message, "suma de los certificados");

        vm.MontoAutorizado = 500m;
        await servicio.UpdateAsync(vm);
        Assert.IsNotNull(guardado);
        Assert.AreEqual(10, guardado.ObraId, "la obra no se cambia desde la edición");
        Assert.AreEqual("OBRA-1", guardado.CodigoAutorizante);
        Assert.IsTrue(guardado.AutorizacionGG);
        Assert.AreEqual(7, guardado.ConceptoId);
        Assert.AreEqual(500m, guardado.MontoAutorizado, "igual a lo certificado se admite");
        Assert.IsNull(guardado.Obra, "sin navegaciones");

        vm.Id = 77;
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => servicio.UpdateAsync(vm));

        var virtual_ = new AutorizanteViewModel { Id = 0, RedeterminacionId = 5 };
        var ex3 = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.UpdateAsync(virtual_));
        StringAssert.Contains(ex3.Message, "propia página");
    }

    [TestMethod]
    public async Task Delete_SeBloqueaConCertificadosORedeterminaciones()
    {
        var armado = new Armado(Usuario(admin: true));
        armado.Repo.Setup(r => r.TieneDependenciasAsync("OBRA-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => armado.Servicio().DeleteAsync(1));
        StringAssert.Contains(ex.Message, "certificados o redeterminaciones");
        armado.Repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        armado.Repo.Setup(r => r.TieneDependenciasAsync("OBRA-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await armado.Servicio().DeleteAsync(1);
        armado.Repo.Verify(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Seguimiento_RequierePermisoInline_YLaFilaBorradaEsConflicto()
    {
        var vm = new AutorizanteViewModel { Id = 1, Expediente = " EX-2 ", EstadoId = 3 };

        var sinPermiso = new Armado(Usuario(admin: false, 26), inline: false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => sinPermiso.Servicio().ActualizarSeguimientoAsync(vm));

        // Con la planificación cerrada igual se edita (dictamen del 04/11/2026).
        var armado = new Armado(Usuario(admin: false, 26), planificacionAbierta: false);
        armado.Repo.Setup(r => r.UpdateSeguimientoAsync(1, "EX-2", 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await armado.Servicio().ActualizarSeguimientoAsync(vm);
        armado.Repo.Verify(r => r.UpdateSeguimientoAsync(1, "EX-2", 3, It.IsAny<CancellationToken>()), Times.Once);

        armado.Repo.Setup(r => r.UpdateSeguimientoAsync(1, "EX-2", 3, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => armado.Servicio().ActualizarSeguimientoAsync(vm));
    }
}
