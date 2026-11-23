using Bunit;
using IVC.Application.Common;
using IVC.Application.Formulacion;
using IVC.Application.Formulacion.Dtos;
using IVC.Application.Identidad;
using IVC.Services.Abstractions;
using IVC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tests.TestSupport;
using FormulacionPage = IVC.Components.Pages.Formulacion.Formulacion;

namespace Tests.Components;

/// <summary>
/// Render de la página Formulación: la grilla con los años del ciclo en los títulos, el PPI
/// sólo para el administrador y el alta sólo con la formulación abierta.
/// </summary>
[TestClass]
public class FormulacionPageRenderTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private static Bunit.TestContext Contexto(UsuarioIvc usuario, bool puedeModificar)
    {
        var ctx = new Bunit.TestContext();
        ctx.AddPermissivePermissions();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(Mock.Of<INotificationHelper>());
        ctx.Services.AddSingleton(Mock.Of<IExportService>());
        ctx.Services.AddSingleton(Mock.Of<IAuditoriaService>());
        ctx.Services.AddSingleton(sp => new Radzen.DialogService(
            sp.GetRequiredService<NavigationManager>(),
            sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));

        var servicio = new Mock<IFormulacionService>();
        servicio.SetupGet(s => s.Ciclo).Returns(new FormulacionCiclo(2027));
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new FormulacionViewModel
            {
                ObraId = 1, Obra = "PRUEBA-TORRE", Area = "PRUEBA-AREA", Empresa = "PRUEBA-EMPRESA", Contrata = "LP 12/2024",
                MontoAnio1 = 100m, MontoAnio3 = 300m, Ppi = 77, Techos = 900m, UnidadMedida = "PRUEBA-UNIDAD", Prioridad = "PRUEBA-PRIORIDAD",
            },
        ]);
        ctx.Services.AddSingleton(servicio.Object);

        var lookup = new Mock<ILookupService>();
        lookup.Setup(l => l.GetUnidadesMedidaAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "PRUEBA-UNIDAD")]);
        lookup.Setup(l => l.GetPrioridadesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(2, "PRUEBA-PRIORIDAD")]);
        ctx.Services.AddSingleton(lookup.Object);

        var usuarioActual = new Mock<IUsuarioActual>();
        usuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
        ctx.Services.AddSingleton(usuarioActual.Object);

        var permisos = new Mock<IPermisosIvc>();
        permisos.Setup(p => p.PuedeModificarFormulacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(puedeModificar);
        ctx.Services.AddSingleton(permisos.Object);
        return ctx;
    }

    [TestMethod]
    public void Administrador_VeLosAniosDelCiclo_ElPpi_YElBotonNuevo()
    {
        using var ctx = Contexto(Usuario(admin: true), puedeModificar: true);

        var cut = ctx.RenderComponent<FormulacionPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "rz-data-grid");
            StringAssert.Contains(cut.Markup, "PRUEBA-TORRE");
            StringAssert.Contains(cut.Markup, "Monto 2027");
            StringAssert.Contains(cut.Markup, "Monto 2029");
            StringAssert.Contains(cut.Markup, "Ciclo 2027–2029");
            StringAssert.Contains(cut.Markup, "PPI");
            StringAssert.Contains(cut.Markup, "PRUEBA-PRIORIDAD");
            StringAssert.Contains(cut.Markup, "Nuevo");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void UsuarioNormal_NoVeElPpi_YSinFormulacionAbiertaNoHayAlta()
    {
        using var ctx = Contexto(Usuario(admin: false, 26), puedeModificar: false);

        var cut = ctx.RenderComponent<FormulacionPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PRUEBA-TORRE");
            StringAssert.Contains(cut.Markup, "Monto 2028");
            Assert.IsFalse(cut.Markup.Contains(">PPI<"), "la columna PPI es sólo del administrador");
            Assert.IsFalse(cut.Markup.Contains(">Nuevo<"), "sin alta con la formulación cerrada");
        }, timeout: TimeSpan.FromSeconds(5));
    }
}
