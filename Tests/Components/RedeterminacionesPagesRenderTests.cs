using Bunit;
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using IVC.Services.Abstractions;
using IVC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tests.TestSupport;
using AutorizantesPage = IVC.Components.Pages.Redeterminaciones.Autorizantes;
using CertificadosPage = IVC.Components.Pages.Redeterminaciones.Certificados;
using LegitimosPage = IVC.Components.Pages.Redeterminaciones.Legitimos;
using RedeterminacionesPage = IVC.Components.Pages.Redeterminaciones.Redeterminaciones;

namespace Tests.Components;

/// <summary>
/// Render de las cuatro páginas del módulo de redeterminaciones con servicios simulados:
/// la grilla con sus filas (reales y virtuales), el botón Nuevo según permisos y las
/// columnas que el área Redeterminaciones no ve en Autorizantes.
/// </summary>
[TestClass]
public class RedeterminacionesPagesRenderTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private static Bunit.TestContext Contexto(UsuarioIvc usuario, bool planificacionAbierta, bool puedeRedet = true)
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
        lookup.Setup(l => l.GetObrasAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(10, "PRUEBA-OBRA")]);
        lookup.Setup(l => l.GetConceptosAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(4, "BASICO")]);
        lookup.Setup(l => l.GetEstadosAutorizanteAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "EN EJECUCION")]);
        lookup.Setup(l => l.GetEstadosRedetAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(12, "RD-11/11-Notificada")]);
        lookup.Setup(l => l.GetTiposPagoAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(2, "CERTIFICADO")]);
        lookup.Setup(l => l.GetUsuariosRedeterminacionesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(9, "PRUEBA-USUARIO")]);
        lookup.Setup(l => l.GetAutorizantesAsync(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionLookup(1, "OBRA-1 - PRUEBA-OBRA")]);
        ctx.Services.AddSingleton(lookup.Object);

        var usuarioActual = new Mock<IUsuarioActual>();
        usuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
        ctx.Services.AddSingleton(usuarioActual.Object);

        var permisos = new Mock<IPermisosIvc>();
        permisos.Setup(p => p.PuedeModificarPlanificacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(planificacionAbierta);
        permisos.Setup(p => p.PuedeEditarInlineAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        permisos.Setup(p => p.PuedeModificarRedeterminacionesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(puedeRedet);
        ctx.Services.AddSingleton(permisos.Object);

        var autorizantes = new Mock<IAutorizantesService>();
        autorizantes.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AutorizanteViewModel { Id = 1, CodigoAutorizante = "PRUEBA-OBRA-1", Obra = "PRUEBA-OBRA", Area = "PRUEBA-AREA", MesBase = new DateTime(2024, 1, 1), BuzonSade = "PRUEBA-BUZON", MontoAutorizado = 1000m },
            new AutorizanteViewModel { Id = 0, RedeterminacionId = 5, CodigoAutorizante = "PRUEBA-OBRA-1-R1", Obra = "PRUEBA-OBRA", Estado = "APROBADO", Concepto = "REDETERMINACION", MontoAutorizado = 80m },
        ]);
        ctx.Services.AddSingleton(autorizantes.Object);

        var certificados = new Mock<ICertificadosService>();
        certificados.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CertificadoViewModel { Id = 1, CodigoAutorizante = "PRUEBA-OBRA-1", Obra = "PRUEBA-OBRA", TipoPago = "CERTIFICADO", MontoTotal = 200m, Sigaf = 150m, Estado = "DEVENGADO" },
            new CertificadoViewModel { Id = 0, TipoPagoId = ReglasRedeterminaciones.TipoPagoReliquidacionId, CodigoAutorizante = "PRUEBA-OBRA-1-R1", Obra = "PRUEBA-OBRA", TipoPago = "RELIQUIDACION", MontoTotal = 40m, MesAprobacion = new DateTime(2025, 7, 1) },
        ]);
        ctx.Services.AddSingleton(certificados.Object);

        var legitimos = new Mock<ILegitimosService>();
        legitimos.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [new LegitimoViewModel { Id = 1, CodigoAutorizante = "PRUEBA-LEGITIMO", Obra = "PRUEBA-OBRA", Certificado = 500m, Sigaf = 123m, Estado = "DEVENGADO" }]);
        ctx.Services.AddSingleton(legitimos.Object);

        var redets = new Mock<IRedeterminacionesService>();
        redets.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new RedeterminacionViewModel { Id = 5, CodigoRedet = "PRUEBA-OBRA-1-R1", Obra = "PRUEBA-OBRA", Etapa = "RD-02/11-Análisis Tecnica", DiasEnBuzon = 10, Discrepancia = true, Usuario = "PRUEBA-USUARIO" },
            new RedeterminacionViewModel { Id = 6, CodigoRedet = "PRUEBA-OBRA-1-R2", Obra = "PRUEBA-OBRA", Etapa = "ACDIR", DiasEnBuzon = null, Discrepancia = false },
        ]);
        ctx.Services.AddSingleton(redets.Object);
        return ctx;
    }

    [TestMethod]
    public void Autorizantes_Admin_VeRealesYVirtuales_YElBotonNuevo()
    {
        using var ctx = Contexto(Usuario(admin: true), planificacionAbierta: true);
        var cut = ctx.RenderComponent<AutorizantesPage>();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "rz-data-grid");
            StringAssert.Contains(cut.Markup, "PRUEBA-OBRA-1-R1");
            StringAssert.Contains(cut.Markup, "REDETERMINACION");
            StringAssert.Contains(cut.Markup, "Mes base");
            StringAssert.Contains(cut.Markup, "PRUEBA-BUZON");
            StringAssert.Contains(cut.Markup, ">Nuevo<");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Autorizantes_AreaRedeterminaciones_NoVeMesBaseNiSade_NiPuedeCrear()
    {
        using var ctx = Contexto(Usuario(admin: false, IvcAreaIds.Redeterminaciones), planificacionAbierta: false);
        var cut = ctx.RenderComponent<AutorizantesPage>();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PRUEBA-OBRA-1");
            Assert.IsFalse(cut.Markup.Contains("Mes base"), "columna oculta al área Redeterminaciones");
            Assert.IsFalse(cut.Markup.Contains("PRUEBA-BUZON"), "SADE oculto al área Redeterminaciones");
            Assert.IsFalse(cut.Markup.Contains(">Nuevo<"));
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Certificados_VeCertificadosYReliquidaciones_ConSigaf()
    {
        using var ctx = Contexto(Usuario(admin: true), planificacionAbierta: true);
        var cut = ctx.RenderComponent<CertificadosPage>();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PRUEBA-OBRA-1-R1");
            StringAssert.Contains(cut.Markup, "RELIQUIDACION");
            StringAssert.Contains(cut.Markup, "SIGAF");
            StringAssert.Contains(cut.Markup, "DEVENGADO");
            StringAssert.Contains(cut.Markup, ">Nuevo<");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Legitimos_VeLaGrilla_YSinPlanificacionNoHayAlta()
    {
        using var ctx = Contexto(Usuario(admin: false, 26), planificacionAbierta: false);
        var cut = ctx.RenderComponent<LegitimosPage>();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PRUEBA-LEGITIMO");
            StringAssert.Contains(cut.Markup, "Monto certificado");
            Assert.IsFalse(cut.Markup.Contains(">Nuevo<"), "sin planificación abierta no hay alta");
            StringAssert.Contains(cut.Markup, "title=\"Editar fila\"", "el inline sigue disponible");
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Redeterminaciones_VeDiasEnBuzonYDiscrepancia_YSecretariaNoEdita()
    {
        using var ctx = Contexto(Usuario(admin: true), planificacionAbierta: true);
        var cut = ctx.RenderComponent<RedeterminacionesPage>();
        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PRUEBA-OBRA-1-R1");
            StringAssert.Contains(cut.Markup, "Control de estados");
            StringAssert.Contains(cut.Markup, "Días x buzón");
            StringAssert.Contains(cut.Markup, "El buzón SADE no corresponde a la etapa");
            StringAssert.Contains(cut.Markup, ">10</span>");
            StringAssert.Contains(cut.Markup, ">Nuevo<");
        }, timeout: TimeSpan.FromSeconds(5));

        using var ctxSecretaria = Contexto(Usuario(admin: false, IvcAreaIds.Secretaria), planificacionAbierta: true, puedeRedet: false);
        var soloLectura = ctxSecretaria.RenderComponent<RedeterminacionesPage>();
        soloLectura.WaitForAssertion(() =>
        {
            StringAssert.Contains(soloLectura.Markup, "PRUEBA-OBRA-1-R2");
            Assert.IsFalse(soloLectura.Markup.Contains(">Nuevo<"));
            Assert.IsFalse(soloLectura.Markup.Contains("title=\"Editar fila\""));
        }, timeout: TimeSpan.FromSeconds(5));
    }
}
