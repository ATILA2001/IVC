using Microsoft.VisualStudio.TestTools.UnitTesting;
using IVC.Application.Common;

namespace Tests.Application;

[TestClass]
public class ValidacionesTests
{
    [TestMethod]
    public void Largo_ExcedeElMaximo_AgregaError()
    {
        var errores = new List<string>();
        Validaciones.Largo(errores, new string('x', 11), 10, "Campo");
        Assert.AreEqual(1, errores.Count);
        StringAssert.Contains(errores[0], "Campo");
        StringAssert.Contains(errores[0], "10");
    }

    [TestMethod]
    public void Largo_EnElLimiteONull_NoAgregaError()
    {
        var errores = new List<string>();
        Validaciones.Largo(errores, new string('x', 10), 10, "Campo");
        Validaciones.Largo(errores, null, 10, "Campo");
        Assert.AreEqual(0, errores.Count);
    }




    [TestMethod]
    public void ImporteNoNegativo_SoloRechazaNegativos()
    {
        var errores = new List<string>();
        Validaciones.ImporteNoNegativo(errores, -0.01m, "Importe");
        Assert.AreEqual(1, errores.Count);

        errores.Clear();
        Validaciones.ImporteNoNegativo(errores, 0m, "Importe");
        Validaciones.ImporteNoNegativo(errores, null, "Importe");
        Validaciones.ImporteNoNegativo(errores, 100m, "Importe");
        Assert.AreEqual(0, errores.Count);
    }
}
