using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// El panel de configuración persiste cada interruptor por separado, audita sólo los
/// cambios reales y nunca deja que una falla de auditoría voltee el cambio.
/// </summary>
[TestClass]
public class ConfiguracionServiceTests
{
    private static (Mock<IAbmPlaniRepository> Plani, Mock<IUsuarioVinculadoRepository> Vinculados,
        Mock<IAuditoriaService> Auditoria, ConfiguracionService Servicio) Armar(EstadoPlanificacion estado)
    {
        var plani = new Mock<IAbmPlaniRepository>();
        plani.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estado);
        var vinculados = new Mock<IUsuarioVinculadoRepository>();
        var auditoria = new Mock<IAuditoriaService>();
        var servicio = new ConfiguracionService(plani.Object, vinculados.Object, auditoria.Object,
            NullLogger<ConfiguracionService>.Instance);
        return (plani, vinculados, auditoria, servicio);
    }

    [TestMethod]
    public async Task AbrirPlanificacion_PersisteSoloEseInterruptor_YAudita()
    {
        var (plani, _, auditoria, servicio) = Armar(new EstadoPlanificacion(false, true));

        await servicio.SetPlanificacionAbiertaAsync(true, "ana");

        plani.Verify(p => p.SetAsync(new EstadoPlanificacion(true, true), It.IsAny<CancellationToken>()), Times.Once);
        auditoria.Verify(a => a.RegistrarAsync(It.Is<RegistroAuditoria>(r =>
            r.Vista == ConfiguracionService.Vista && r.Usuario == "ana" && r.ClaveNegocio == "Planificación"
            && r.Cambios.Count == 1 && r.Cambios[0].Antes == "No" && r.Cambios[0].Despues == "Sí"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SinCambioReal_NoPersisteNiAudita()
    {
        var (plani, _, auditoria, servicio) = Armar(new EstadoPlanificacion(true, false));

        await servicio.SetPlanificacionAbiertaAsync(true, "ana");
        await servicio.SetFormulacionAbiertaAsync(false, "ana");

        plani.Verify(p => p.SetAsync(It.IsAny<EstadoPlanificacion>(), It.IsAny<CancellationToken>()), Times.Never);
        auditoria.Verify(a => a.RegistrarAsync(It.IsAny<RegistroAuditoria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SiLaAuditoriaFalla_ElCambioQuedaGuardado()
    {
        var (plani, _, auditoria, servicio) = Armar(EstadoPlanificacion.Cerrado);
        auditoria.Setup(a => a.RegistrarAsync(It.IsAny<RegistroAuditoria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sin tabla"));

        await servicio.SetFormulacionAbiertaAsync(true, "ana");

        plani.Verify(p => p.SetAsync(new EstadoPlanificacion(false, true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task OverridePorUsuario_PersisteYAuditaConElNombre()
    {
        var (_, vinculados, auditoria, servicio) = Armar(EstadoPlanificacion.Cerrado);
        vinculados.Setup(v => v.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UsuarioVinculado>
        {
            new() { AuthUserId = "u7", Nombre = "Bruno", PlanificacionAbierta = false },
        });
        vinculados.Setup(v => v.SetPlanificacionAbiertaAsync("u7", true, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await servicio.SetOverridePlanificacionAsync("u7", true, "ana");

        vinculados.Verify(v => v.SetPlanificacionAbiertaAsync("u7", true, It.IsAny<CancellationToken>()), Times.Once);
        auditoria.Verify(a => a.RegistrarAsync(It.Is<RegistroAuditoria>(r =>
            r.ClaveNegocio == "Usuario Bruno" && r.Cambios[0].Campo == "Planificación habilitada"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task OverrideDeUsuarioInexistente_EsConflicto()
    {
        var (_, vinculados, _, servicio) = Armar(EstadoPlanificacion.Cerrado);
        vinculados.Setup(v => v.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UsuarioVinculado>());

        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(
            () => servicio.SetOverridePlanificacionAsync("no-existe", true, "ana"));
    }

    [TestMethod]
    public async Task ListarUsuarios_MapeaAlViewModel()
    {
        var (_, vinculados, _, servicio) = Armar(EstadoPlanificacion.Cerrado);
        vinculados.Setup(v => v.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<UsuarioVinculado>
        {
            new() { AuthUserId = "u1", Nombre = "Ana", PlanificacionAbierta = true },
        });

        var lista = await servicio.ListarUsuariosAsync();

        Assert.AreEqual(1, lista.Count);
        Assert.AreEqual("u1", lista[0].AuthUserId);
        Assert.AreEqual("Ana", lista[0].Nombre);
        Assert.IsTrue(lista[0].PlanificacionAbierta);
    }
}
