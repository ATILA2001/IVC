using Bunit;
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Obras.Dtos;
using IVC.Services.Abstractions;
using IVC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tests.TestSupport;
using ObrasPage = IVC.Components.Pages.Obras.Obras;

namespace Tests.Components;

/// <summary>
/// Render de la página Obras: la grilla con sus filas e indicadores para un administrador,
/// y la vista recortada del área Redeterminaciones (sin columnas financieras ni alta).
/// </summary>
[TestClass]
public class ObrasPageRenderTests
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

        var servicio = new Mock<IObrasService>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new ObraViewModel
            {
                Id = 1, Descripcion = "PRUEBA-TORRE", Area = "PRUEBA-AREA", Empresa = "PRUEBA-EMPRESA", Contrata = "LP", Numero = 12, Anio = 2024,
                Barrio = "PRUEBA-BARRIO", Autorizado2026 = 2000m, MontoCertificado = 400m, Porcentaje = 20m, EjecFisica = 50m, FaltanteEjecFisica = 50m,
            },
        ]);
        servicio.Setup(s => s.GetAreasPermitidasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "PRUEBA-AREA")]);
        ctx.Services.AddSingleton(servicio.Object);

        var lookup = new Mock<ILookupService>();
        lookup.Setup(l => l.GetEmpresasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "PRUEBA-EMPRESA")]);
        lookup.Setup(l => l.GetContratasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "LP")]);
        lookup.Setup(l => l.GetBarriosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "PRUEBA-BARRIO")]);
        ctx.Services.AddSingleton(lookup.Object);

        var usuarioActual = new Mock<IUsuarioActual>();
        usuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
        ctx.Services.AddSingleton(usuarioActual.Object);

        var permisos = new Mock<IPermisosIvc>();
        permisos.Setup(p => p.PuedeModificarPlanificacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(puedeModificar);
        ctx.Services.AddSingleton(permisos.Object);
        return ctx;
    }

    [TestMethod]
    public void Administrador_VeLaGrillaConIndicadoresYElBotonNuevo()
    {
        using var ctx = Contexto(Usuario(admin: true), puedeModificar: true);

        var cut = ctx.RenderComponent<ObrasPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "rz-data-grid");
            StringAssert.Contains(cut.Markup, "PRUEBA-TORRE");
            StringAssert.Contains(cut.Markup, "LP 12/2024");
            StringAssert.Contains(cut.Markup, "Disponible actual");
            StringAssert.Contains(cut.Markup, "Monto certificado");
            StringAssert.Contains(cut.Markup, "Nuevo");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Redeterminaciones_NoVeColumnasFinancieras_NiPuedeCrear()
    {
        using var ctx = Contexto(Usuario(admin: false, IvcAreaIds.Redeterminaciones), puedeModificar: false);

        var cut = ctx.RenderComponent<ObrasPage>();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PRUEBA-TORRE");
            StringAssert.Contains(cut.Markup, "Ejec. física");
            Assert.IsFalse(cut.Markup.Contains("Disponible actual"), "columna financiera oculta");
            Assert.IsFalse(cut.Markup.Contains("Monto certificado"), "columna financiera oculta");
            Assert.IsFalse(cut.Markup.Contains(">Nuevo<"), "sin alta con la planificación cerrada o fuera de las áreas que planifican");
        }, timeout: TimeSpan.FromSeconds(5));
    }
}
