using Bunit;
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Data.Entities;
using IVC.Services.Abstractions;
using IVC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tests.TestSupport;
using Paginas = IVC.Components.Pages.Catalogos;

namespace Tests.Components;

/// <summary>
/// Regresión de render de las seis páginas de catálogos: toolbar con título, grilla
/// con sus encabezados y las filas que devuelve el servicio. Mismo criterio que
/// ConfiguracionPageRenderTests.
/// </summary>
[TestClass]
public class CatalogosPagesRenderTests
{
    private static Bunit.TestContext Contexto()
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
        var lookup = new Mock<ILookupService>();
        lookup.Setup(l => l.GetObrasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpcionLookup(10, "PRUEBA-OBRA")]);
        lookup.Setup(l => l.GetObrasSinProyectoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpcionLookup(20, "PRUEBA-OBRA-LIBRE")]);
        lookup.Setup(l => l.GetLineasGestionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpcionLookup(1, "PRUEBA-LINEA")]);
        ctx.Services.AddSingleton(lookup.Object);
        return ctx;
    }

    private static void Esperar(IRenderedFragment cut, params string[] textos) =>
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "rz-data-grid");
            foreach (var texto in textos)
                StringAssert.Contains(cut.Markup, texto);
        }, timeout: TimeSpan.FromSeconds(5));

    [TestMethod]
    public void Barrios_RenderizaLaGrillaConSusFilas()
    {
        using var ctx = Contexto();
        var servicio = new Mock<ICatalogoSimpleService<Barrio>>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogoSimpleViewModel { Id = 1, Nombre = "PRUEBA-LUGANO" }]);
        ctx.Services.AddSingleton(servicio.Object);

        var cut = ctx.RenderComponent<Paginas.Barrios>();

        Esperar(cut, "Barrios", "PRUEBA-LUGANO");
    }

    [TestMethod]
    public void Empresas_RenderizaLaGrillaConSusFilas()
    {
        using var ctx = Contexto();
        var servicio = new Mock<ICatalogoSimpleService<Empresa>>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CatalogoSimpleViewModel { Id = 1, Nombre = "PRUEBA-CONSTRUCTORA" }]);
        ctx.Services.AddSingleton(servicio.Object);

        var cut = ctx.RenderComponent<Paginas.Empresas>();

        Esperar(cut, "Empresas", "PRUEBA-CONSTRUCTORA");
    }

    [TestMethod]
    public void LineasGestion_RenderizaLasCuatroColumnas()
    {
        using var ctx = Contexto();
        var servicio = new Mock<ILineasGestionService>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LineaGestionViewModel { Id = 1, Nombre = "PRUEBA-VIVIENDA", Tipo = "T", Grupo = "G", Reparticion = "PRUEBA-REP" }]);
        ctx.Services.AddSingleton(servicio.Object);

        var cut = ctx.RenderComponent<Paginas.LineasGestion>();

        Esperar(cut, "Líneas de gestión", "Repartición", "PRUEBA-VIVIENDA", "PRUEBA-REP");
    }

    [TestMethod]
    public void LineasGestionFF_RenderizaLaFuenteConLosDatosDeSuLinea()
    {
        using var ctx = Contexto();
        var servicio = new Mock<ILineasGestionFFService>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LineaGestionFFViewModel { Id = 1, LineaGestionId = 1, LineaGestionNombre = "PRUEBA-LINEA", Nombre = "PRUEBA-FF", Fuente = "PRUEBA-TESORO", Tipo = "T" }]);
        ctx.Services.AddSingleton(servicio.Object);

        var cut = ctx.RenderComponent<Paginas.LineasGestionFF>();

        Esperar(cut, "Líneas de gestión FF", "Nombre FF", "PRUEBA-LINEA", "PRUEBA-FF", "PRUEBA-TESORO");
    }

    [TestMethod]
    public void Proyectos_RenderizaObraCatalogosYMontos()
    {
        using var ctx = Contexto();
        var servicio = new Mock<IProyectosService>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProyectoViewModel
            {
                Id = 1, ObraId = 10, Area = "PRUEBA-AREA", Contrata = "LP 12/2024", Obra = "PRUEBA-TORRE - LUGANO",
                Empresa = "PRUEBA-EMPRESA", Nombre = "PRUEBA-PROYECTO", SubProyecto = "SUB", LineaGestionId = 1,
                LineaGestionNombre = "PRUEBA-LINEA", Autorizado2025 = 1000m, Autorizado2026 = 1500.5m,
            }]);
        ctx.Services.AddSingleton(servicio.Object);

        var cut = ctx.RenderComponent<Paginas.Proyectos>();

        Esperar(cut, "Proyectos", "Autorizado 2026", "PRUEBA-AREA", "PRUEBA-TORRE", "PRUEBA-PROYECTO", "PRUEBA-LINEA");
    }

    [TestMethod]
    public void Movimientos_RenderizaObraMontoFechaYAutorizadoNuevo()
    {
        using var ctx = Contexto();
        var servicio = new Mock<IMovimientosService>();
        servicio.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MovimientoViewModel
            {
                Id = 1, ObraId = 10, Obra = "PRUEBA-TORRE - LUGANO", Proyecto = "PRUEBA-PROYECTO", Linea = "PRUEBA-LINEA",
                Monto = 100m, Fecha = new DateTime(2026, 3, 15), Autorizado2026 = 1100m,
            }]);
        ctx.Services.AddSingleton(servicio.Object);

        var cut = ctx.RenderComponent<Paginas.Movimientos>();

        Esperar(cut, "Movimientos de gestión", "Autorizado nuevo", "PRUEBA-TORRE", "PRUEBA-PROYECTO", "15/03/2026");
    }
}
