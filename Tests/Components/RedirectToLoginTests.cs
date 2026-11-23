using Bunit;
using Bunit.TestDoubles;
using IVC.Components.Account.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Components;

/// <summary>
/// Cuando la sesión expira con el circuito vivo, el componente que manda al login tiene que
/// pasar la ruta relativa de la página actual: es lo único que Login reenvía a Auth.Web
/// (Security/ReturnUrls), así el usuario vuelve a donde estaba.
/// </summary>
[TestClass]
public class RedirectToLoginTests
{
    [TestMethod]
    public void MandaAlLogin_ConLaRutaRelativaDeLaPaginaActual()
    {
        using var ctx = new Bunit.TestContext();
        var nav = ctx.Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("certificados?x=1");

        ctx.RenderComponent<RedirectToLogin>();

        Assert.AreEqual($"{nav.BaseUri}Account/Login?returnUrl={Uri.EscapeDataString("/certificados?x=1")}", nav.Uri);
    }
}
