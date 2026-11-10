using Bunit;
using IVC.Application.Identidad;
using IVC.Application.Identidad.Dtos;
using IVC.Services.Abstractions;
using IVC.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tests.TestSupport;
using ConfiguracionPage = IVC.Components.Pages.Admin.Configuracion;

namespace Tests.Components;

/// <summary>
/// Render del panel de configuración: interruptores globales y, con la planificación
/// cerrada, la grilla de usuarios con su habilitación individual. Un usuario sin rol de
/// administrador ve el aviso y nada más.
/// </summary>
[TestClass]
public class ConfiguracionPageRenderTests
{
    private static Mock<IConfiguracionService> Servicio(EstadoPlanificacion estado, params UsuarioVinculadoViewModel[] usuarios)
    {
        var servicio = new Mock<IConfiguracionService>();
        servicio.Setup(s => s.ObtenerEstadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(estado);
        servicio.Setup(s => s.ListarUsuariosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuarios.ToList());
        return servicio;
    }

    private static Bunit.TestContext Contexto(Mock<IConfiguracionService> servicio, params string[] roles)
    {
        var ctx = new Bunit.TestContext();
        ctx.AddPermissivePermissions(roles);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(servicio.Object);
        ctx.Services.AddSingleton(Mock.Of<INotificationHelper>());
        ctx.Services.AddSingleton(Mock.Of<IUsuarioActual>());
        return ctx;
    }

    [TestMethod]
    public void Administrador_ConPlanificacionCerrada_VeInterruptoresYGrillaDeUsuarios()
    {
        var servicio = Servicio(EstadoPlanificacion.Cerrado,
            new() { AuthUserId = "u1", Nombre = "PRUEBA-ANA", PlanificacionAbierta = true },
            new() { AuthUserId = "u2", Nombre = "PRUEBA-BRUNO", PlanificacionAbierta = false });
        using var ctx = Contexto(servicio, "Admin");

        var cut = ctx.RenderComponent<ConfiguracionPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Planificación abierta");
            StringAssert.Contains(cut.Markup, "Formulación abierta");
            StringAssert.Contains(cut.Markup, "rz-data-grid");
            StringAssert.Contains(cut.Markup, "PRUEBA-ANA");
            StringAssert.Contains(cut.Markup, "PRUEBA-BRUNO");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Administrador_ConPlanificacionAbierta_NoVeLaGrilla_YVeElAviso()
    {
        var servicio = Servicio(new EstadoPlanificacion(true, false),
            new UsuarioVinculadoViewModel { AuthUserId = "u1", Nombre = "PRUEBA-ANA" });
        using var ctx = Contexto(servicio, "Admin");

        var cut = ctx.RenderComponent<ConfiguracionPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "las habilitaciones individuales no tienen efecto");
            Assert.IsFalse(cut.Markup.Contains("PRUEBA-ANA"), "con la planificación abierta la grilla no se muestra");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void UsuarioSinRolAdmin_VeElAviso_YNoSeConsultaLaConfiguracion()
    {
        var servicio = Servicio(EstadoPlanificacion.Cerrado);
        using var ctx = Contexto(servicio);

        var cut = ctx.RenderComponent<ConfiguracionPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "sólo para administradores");
            Assert.IsFalse(cut.Markup.Contains("Planificación abierta"));
        }, timeout: TimeSpan.FromSeconds(5));
        servicio.Verify(s => s.ObtenerEstadoAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
