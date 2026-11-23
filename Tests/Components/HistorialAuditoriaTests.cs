using Bunit;
using IVC.Components.Shared;
using IVC.Data.Entities;
using IVC.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Components;

/// <summary>
/// El diálogo de historial agrupa por lote y, cuando la página se lo pide, omite los campos
/// que el usuario no ve en pantalla (el PPI de Formulación para el no administrador).
/// </summary>
[TestClass]
public class HistorialAuditoriaTests
{
    private static Bunit.TestContext Contexto()
    {
        var ctx = new Bunit.TestContext();
        var lote = Guid.NewGuid();
        var auditoria = new Mock<IAuditoriaService>();
        auditoria.Setup(a => a.GetHistorialAsync("/formulacion", 10, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new CambioAuditoria { Id = 1, Lote = lote, Usuario = "ANA", Accion = "Edición", Vista = "/formulacion", EntidadId = 10, Campo = "PPI", ValorAnterior = "7", ValorNuevo = "99" },
            new CambioAuditoria { Id = 2, Lote = lote, Usuario = "ANA", Accion = "Edición", Vista = "/formulacion", EntidadId = 10, Campo = "Techo", ValorAnterior = "900,00", ValorNuevo = "800,00" },
        ]);
        ctx.Services.AddSingleton(auditoria.Object);
        return ctx;
    }

    [TestMethod]
    public void SinCamposOcultos_MuestraTodosLosCambios()
    {
        using var ctx = Contexto();
        var cut = ctx.RenderComponent<HistorialAuditoria>(p => p
            .Add(h => h.Vista, "/formulacion").Add(h => h.EntidadId, 10).Add(h => h.ClaveNegocio, "TORRE"));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "PPI");
            StringAssert.Contains(cut.Markup, "Techo");
            StringAssert.Contains(cut.Markup, "Edición");
        });
    }

    [TestMethod]
    public void ConCamposOcultos_OmiteEsosCampos()
    {
        using var ctx = Contexto();
        var cut = ctx.RenderComponent<HistorialAuditoria>(p => p
            .Add(h => h.Vista, "/formulacion").Add(h => h.EntidadId, 10).Add(h => h.ClaveNegocio, "TORRE")
            .Add(h => h.CamposOcultos, new[] { "ppi" }));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Techo");
            Assert.IsFalse(cut.Markup.Contains("PPI"), "el campo oculto no se lista (sin distinguir mayúsculas)");
        });
    }
}
