using IVC.Application.Redeterminaciones;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

/// <summary>
/// Mapeo de etapa a estado general (ids según Database/vw_PowerBI_Redeterminaciones.sql del
/// legado), estado y datos SADE de un pago, y correspondencia etapa-buzón.
/// </summary>
[TestClass]
public class EstadoRedetYSadeTests
{
    [TestMethod]
    public void EtapaNull_EsNoIniciado()
    {
        var estado = MapeoEstadoRedet.MapearEstadoDesdeId(null);
        Assert.AreEqual(4, estado.Id);
        Assert.AreEqual("NO INICIADO", estado.Nombre);
    }

    [DataTestMethod]
    [DataRow(37, 4, "NO INICIADO")]
    [DataRow(38, 4, "NO INICIADO")]
    [DataRow(12, 1, "APROBADO")]
    [DataRow(22, 1, "APROBADO")]
    [DataRow(33, 1, "APROBADO")]
    [DataRow(39, 1, "APROBADO")]
    [DataRow(35, 3, "DESESTIMADO")]
    [DataRow(36, 3, "DESESTIMADO")]
    [DataRow(1, 2, "EN TRAMITE")]
    [DataRow(99, 2, "EN TRAMITE")]
    public void EtapaConocida_MapeaAlEstadoGeneral(int etapaId, int estadoEsperado, string nombreEsperado)
    {
        var estado = MapeoEstadoRedet.MapearEstadoDesdeId(etapaId);
        Assert.AreEqual(estadoEsperado, estado.Id);
        Assert.AreEqual(nombreEsperado, estado.Nombre);
    }

    [TestMethod]
    public void EstadosParaFiltro_SonLosCuatro_OrdenadosPorNombre()
    {
        CollectionAssert.AreEqual(
            new[] { "APROBADO", "DESESTIMADO", "EN TRAMITE", "NO INICIADO" },
            MapeoEstadoRedet.ObtenerEstadosParaFiltro().Select(e => e.Nombre).ToArray());
    }

    private static readonly Dictionary<string, UltimoPaseSade> Sade = new()
    {
        ["EX-1"] = new UltimoPaseSade("DGTAL", new DateTime(2026, 5, 2)),
    };

    [TestMethod]
    public void PropiedadesPago_SinExpediente_NoIniciadoYSinSade_AunqueHayaDatosDeOtros()
    {
        var (estado, buzon, fecha) = PropiedadesPago.Calcular(null, null, Sade);
        Assert.AreEqual("NO INICIADO", estado);
        Assert.IsNull(buzon);
        Assert.IsNull(fecha);
    }

    [TestMethod]
    public void PropiedadesPago_ConExpedienteYSigaf_DevengadoConDatosSade()
    {
        var (estado, buzon, fecha) = PropiedadesPago.Calcular("EX-1", 50m, Sade);
        Assert.AreEqual("DEVENGADO", estado);
        Assert.AreEqual("DGTAL", buzon);
        Assert.AreEqual(new DateTime(2026, 5, 2), fecha);
    }

    [TestMethod]
    public void PropiedadesPago_ConExpedienteSinSigafNiSade_EnTramite()
    {
        var (estado, buzon, fecha) = PropiedadesPago.Calcular("EX-2", null, Sade);
        Assert.AreEqual("EN TRAMITE", estado);
        Assert.IsNull(buzon);
        Assert.IsNull(fecha);
    }

    [TestMethod]
    public void BuzonEtapa_DiscrepanciaSoloSiLaEtapaEsConocidaYNingunBuzonCoincide()
    {
        Assert.IsFalse(BuzonEtapaMatcher.IsMismatch("RD-03/11-Análisis DGAyF", "IVC-4010 DEPTO REDETERMINACIONES - Juan Pérez"), "el buzón permitido está contenido en el pase");
        Assert.IsTrue(BuzonEtapaMatcher.IsMismatch("RD-03/11-Análisis DGAyF", "IVC-1850 DEPTO DESPACHOS Y PROYECTOS"));
        Assert.IsTrue(BuzonEtapaMatcher.IsMismatch("RD-02/11-Análisis Tecnica", "IVC-4010 DEPTO REDETERMINACIONES"), "la clave con tilde se conserva del legado");
        Assert.IsFalse(BuzonEtapaMatcher.IsMismatch("Etapa inexistente", "IVC-4010 DEPTO REDETERMINACIONES"));
        Assert.IsFalse(BuzonEtapaMatcher.IsMismatch(null, "IVC-4010 DEPTO REDETERMINACIONES"));
        Assert.IsFalse(BuzonEtapaMatcher.IsMismatch("RD-03/11-Análisis DGAyF", ""));
        // 12 etapas RD, 11 RO, 10 RP y "Rectificacion": las mismas 34 del diccionario del legado.
        Assert.AreEqual(34, BuzonEtapaMatcher.EtapasConocidas.Count(), "las mismas 34 etapas del legado");
    }
}
