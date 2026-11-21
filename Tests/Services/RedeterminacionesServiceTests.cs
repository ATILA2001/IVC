using IVC.Application.Common;
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
/// El servicio de redeterminaciones: cross-area, datos de obra por código de autorizante,
/// días en buzón y control de estados por fila, y las escrituras con el permiso de
/// redeterminaciones (alta con el código del autorizante elegido, edición sin tocar
/// autorizante ni usuario, seguimiento inline con desasignación de usuario).
/// </summary>
[TestClass]
public class RedeterminacionesServiceTests
{
    private static readonly DateTime Hoy = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Hoy);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class Armado
    {
        public Mock<IRedeterminacionRepository> Repo { get; } = new();
        public Mock<IAutorizanteRepository> Autorizantes { get; } = new();
        public Mock<IDatosExternosService> Externos { get; } = new();
        public Mock<IPermisosIvc> Permisos { get; } = new();

        public Armado(bool puedeModificar = true)
        {
            Permisos.Setup(p => p.PuedeModificarRedeterminacionesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(puedeModificar);
            Repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([EnTramite(), Aprobada()]);
            Repo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(EnTramite());
            Autorizantes.Setup(a => a.GetPorCodigosAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([AutorizanteDePrueba()]);
            Autorizantes.Setup(a => a.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(AutorizanteDePrueba());
            Externos.Setup(e => e.GetUltimosPasesSadeAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, UltimoPaseSade>
                {
                    ["EX-R1"] = new("IVC-4010 DEPTO REDETERMINACIONES", Hoy.AddDays(-10)),
                    ["EX-R2"] = new("IVC-4010 DEPTO REDETERMINACIONES", Hoy.AddDays(-3)),
                });
        }

        public RedeterminacionesService Servicio() => new(
            Repo.Object, Autorizantes.Object, Externos.Object, Permisos.Object, new RelojFijo(), NullLogger<RedeterminacionesService>.Instance);
    }

    private static Autorizante AutorizanteDePrueba() => new()
    {
        Id = 1, CodigoAutorizante = "OBRA-1", MontoAutorizado = 1000m, ObraId = 10,
        Obra = new Obra
        {
            Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024, AreaId = 26, Area = new Area { Id = 26, Nombre = "DGDH" },
            EmpresaId = 2, Empresa = new Empresa { Id = 2, Nombre = "CONSTRUCTORA" }, Contrata = new Contrata { Id = 3, Nombre = "LP" },
        },
    };

    private static Redeterminacion EnTramite() => new()
    {
        Id = 5, CodigoAutorizante = "OBRA-1", CodigoRedet = "OBRA-1-R1", Nro = 1, Porcentaje = 10m, Salto = new DateTime(2025, 6, 1), Tipo = "PROVISORIA",
        Expediente = "EX-R1", Observaciones = "obs", EtapaId = 2, Etapa = new EstadoRedet { Id = 2, Nombre = "RD-02/11-Análisis Tecnica" },
        UsuarioId = 9, Usuario = new Usuario { Id = 9, Nombre = "ANA" }, Monto = 1m,
    };

    private static Redeterminacion Aprobada() => new()
    {
        Id = 6, CodigoAutorizante = "OBRA-1", CodigoRedet = "OBRA-1-R2", Nro = 2, Porcentaje = 5m, Salto = new DateTime(2026, 1, 1), Tipo = "DEFINITIVA",
        Expediente = "EX-R2", EtapaId = 39, Etapa = new EstadoRedet { Id = 39, Nombre = "ACDIR" },
    };

    [TestMethod]
    public async Task GetAll_CompletaObraDelAutorizante_DiasEnBuzon_YControlDeEstados()
    {
        var filas = await new Armado().Servicio().GetAllAsync();

        Assert.AreEqual(2, filas.Count);
        var enTramite = filas.Single(f => f.Id == 5);
        Assert.AreEqual("OBRA-1-R1", enTramite.CodigoRedet);
        Assert.AreEqual(1, enTramite.AutorizanteId);
        Assert.AreEqual("TORRE A", enTramite.Obra);
        Assert.AreEqual("CONSTRUCTORA", enTramite.Empresa);
        Assert.AreEqual("LP 12/2024", enTramite.Contrata);
        Assert.AreEqual("DGDH", enTramite.Area);
        Assert.AreEqual("RD-02/11-Análisis Tecnica", enTramite.Etapa);
        Assert.AreEqual("ANA", enTramite.Usuario);
        Assert.AreEqual(10, enTramite.DiasEnBuzon);
        Assert.IsTrue(enTramite.Discrepancia, "análisis técnica con el expediente en el buzón de Redeterminaciones");

        var aprobada = filas.Single(f => f.Id == 6);
        Assert.IsNull(aprobada.DiasEnBuzon, "las etapas terminales no se siguen");
        Assert.IsFalse(aprobada.Discrepancia, "etapa sin correspondencia: nunca marca");
        Assert.AreEqual(new DateTime(2026, 11, 7, 12, 0, 0), aprobada.FechaSade);
        Assert.IsNull(aprobada.Usuario);
    }

    [TestMethod]
    public async Task Create_RequierePermiso_YTomaElCodigoDelAutorizanteElegido()
    {
        var vm = new RedeterminacionViewModel
        {
            AutorizanteId = 1, Expediente = " EX-N ", Salto = new DateTime(2026, 3, 1), Nro = 3, Tipo = " DEFINITIVA ", EtapaId = 37, Porcentaje = 7.5m, UsuarioId = 9,
        };
        var sinPermiso = new Armado(puedeModificar: false);
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => sinPermiso.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex.Message, "permiso");

        var armado = new Armado();
        armado.Repo.Setup(r => r.AddAsync(It.IsAny<Redeterminacion>(), It.IsAny<CancellationToken>()))
            .Callback<Redeterminacion, CancellationToken>((r, _) => { r.Id = 77; r.CodigoRedet = "OBRA-1-R3"; }).Returns(Task.CompletedTask);
        armado.Repo.Setup(r => r.GetByIdAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Redeterminacion { Id = 77, CodigoAutorizante = "OBRA-1", CodigoRedet = "OBRA-1-R3", EtapaId = 37, Etapa = new EstadoRedet { Id = 37, Nombre = "Pendiente" } });
        var creada = await armado.Servicio().CreateAsync(vm);
        armado.Repo.Verify(r => r.AddAsync(It.Is<Redeterminacion>(r =>
            r.CodigoAutorizante == "OBRA-1" && r.Expediente == "EX-N" && r.Tipo == "DEFINITIVA" && r.Nro == 3 && r.UsuarioId == null), It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual("OBRA-1-R3", creada.CodigoRedet, "vuelve releída con la columna calculada");
        Assert.AreEqual("TORRE A", creada.Obra);

        vm.AutorizanteId = 404;
        var ex2 = await Assert.ThrowsExceptionAsync<ArgumentException>(() => armado.Servicio().CreateAsync(vm));
        StringAssert.Contains(ex2.Message, "no existe");
    }

    [TestMethod]
    public async Task Update_ConservaAutorizanteYUsuario_YLaFilaBorradaEsConflicto()
    {
        var armado = new Armado();
        Redeterminacion? guardada = null;
        armado.Repo.Setup(r => r.UpdateAsync(It.IsAny<Redeterminacion>(), It.IsAny<CancellationToken>()))
            .Callback<Redeterminacion, CancellationToken>((r, _) => guardada = r).Returns(Task.CompletedTask);
        var vm = new RedeterminacionViewModel
        {
            Id = 5, AutorizanteId = 999, UsuarioId = null, Expediente = "EX-E", Salto = new DateTime(2025, 7, 1), Nro = 1, Tipo = "DEFINITIVA", EtapaId = 3, Porcentaje = 11m, Observaciones = " o ",
        };
        await armado.Servicio().UpdateAsync(vm);
        Assert.IsNotNull(guardada);
        Assert.AreEqual("OBRA-1", guardada.CodigoAutorizante);
        Assert.AreEqual(9, guardada.UsuarioId, "el usuario asignado se maneja inline");
        Assert.AreEqual(1m, guardada.Monto, "columna sin uso que no se pierde");
        Assert.AreEqual("o", guardada.Observaciones);
        Assert.AreEqual(3, guardada.EtapaId);
        Assert.IsNull(guardada.Etapa, "sin navegaciones");

        vm.Id = 404;
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => armado.Servicio().UpdateAsync(vm));
    }

    [TestMethod]
    public async Task Seguimiento_DesasignaElUsuarioCuandoVieneVacio()
    {
        var armado = new Armado();
        armado.Repo.Setup(r => r.UpdateSeguimientoAsync(5, 3, "EX-S", null, true, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        armado.Repo.Setup(r => r.UpdateSeguimientoAsync(5, 3, "", 9, false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var servicio = armado.Servicio();

        await servicio.ActualizarSeguimientoAsync(new RedeterminacionViewModel { Id = 5, EtapaId = 3, Expediente = " EX-S ", UsuarioId = null });
        armado.Repo.Verify(r => r.UpdateSeguimientoAsync(5, 3, "EX-S", null, true, It.IsAny<CancellationToken>()), Times.Once);

        await servicio.ActualizarSeguimientoAsync(new RedeterminacionViewModel { Id = 5, EtapaId = 3, Expediente = null, UsuarioId = 9 });
        armado.Repo.Verify(r => r.UpdateSeguimientoAsync(5, 3, "", 9, false, It.IsAny<CancellationToken>()), Times.Once);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.ActualizarSeguimientoAsync(new RedeterminacionViewModel { Id = 5, EtapaId = null }));

        armado.Repo.Setup(r => r.UpdateSeguimientoAsync(5, 3, "EX-S", null, true, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() =>
            servicio.ActualizarSeguimientoAsync(new RedeterminacionViewModel { Id = 5, EtapaId = 3, Expediente = "EX-S" }));

        var sinPermiso = new Armado(puedeModificar: false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => sinPermiso.Servicio().DeleteAsync(5));
    }
}
