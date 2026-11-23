using IVC.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Security;

/// <summary>
/// El returnUrl que se reenvía al login central: sólo rutas relativas de la propia app.
/// </summary>
[TestClass]
public class ReturnUrlsTests
{
    [DataTestMethod]
    [DataRow("/")]
    [DataRow("/obras")]
    [DataRow("/formulacion?x=1")]
    [DataRow("/admin/configuracion#top")]
    public void RutasRelativasDeLaApp_SonLocales(string url) => Assert.IsTrue(ReturnUrls.EsLocal(url));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("obras")]
    [DataRow("https://otro.sitio/obras")]
    [DataRow("//otro.sitio/obras")]
    [DataRow("/\\otro.sitio")]
    [DataRow("/obras\r\nSet-Cookie: x")]
    public void OtrosHostsEsquemasOCaracteresDeControl_NoSonLocales(string? url) => Assert.IsFalse(ReturnUrls.EsLocal(url));
}
