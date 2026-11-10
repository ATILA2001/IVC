using IVC.Application.Identidad;
using IVC.Application.Obras;
using IVC.Application.Obras.Dtos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

/// <summary>
/// Las fórmulas de CalculoObraNegocioEF del legado, sin cambios: montos certificado,
/// inicial, actual y faltante, porcentaje sobre el autorizado 2026, fechas extremas y
/// ejecución física ponderada por monto autorizado.
/// </summary>
[TestClass]
public class IndicadoresObraTests
{
    private static readonly AgregadosObra Muestra = new(
        SumaAutorizantes: 1000m,
        SumaAutorizantesConcepto4: 600m,
        SumaLegitimos: 100m,
        MinLegitimo: new DateTime(2025, 3, 1),
        MaxLegitimo: new DateTime(2025, 9, 1),
        SumaCertificados: 300m,
        MinCertificado: new DateTime(2025, 5, 1),
        MaxCertificado: new DateTime(2026, 1, 1),
        Autorizantes: [new(1000m, 50m), new(3000m, 10m)]);

    [TestMethod]
    public void Montos_CertificadoInicialActualYFaltante_ComoElLegado()
    {
        var i = IndicadoresObra.Calcular(Muestra, autorizado2026: 2000m);

        Assert.AreEqual(400m, i.MontoCertificado, "certificados + legítimos");
        Assert.AreEqual(700m, i.MontoInicial, "autorizantes de concepto 4 + legítimos");
        Assert.AreEqual(1100m, i.MontoActual, "todos los autorizantes + legítimos");
        Assert.AreEqual(700m, i.MontoFaltante, "actual - certificado");
    }

    [TestMethod]
    public void Porcentaje_SobreElAutorizado2026_SoloSiEsMayorQueCero()
    {
        Assert.AreEqual(20m, IndicadoresObra.Calcular(Muestra, 2000m).Porcentaje, "400 / 2000 * 100");
        Assert.IsNull(IndicadoresObra.Calcular(Muestra, null).Porcentaje, "sin proyecto no hay base");
        Assert.IsNull(IndicadoresObra.Calcular(Muestra, 0m).Porcentaje);
        Assert.IsNull(IndicadoresObra.Calcular(Muestra, -5m).Porcentaje);
    }

    [TestMethod]
    public void Fechas_ExtremosEntreCertificadosYLegitimos()
    {
        var i = IndicadoresObra.Calcular(Muestra, 2000m);
        Assert.AreEqual(new DateTime(2025, 3, 1), i.FechaInicio, "el mínimo viene de los legítimos");
        Assert.AreEqual(new DateTime(2026, 1, 1), i.FechaFin, "el máximo viene de los certificados");

        var soloCert = IndicadoresObra.Calcular(Muestra with { MinLegitimo = null, MaxLegitimo = null }, null);
        Assert.AreEqual(new DateTime(2025, 5, 1), soloCert.FechaInicio);
        Assert.IsNull(IndicadoresObra.Calcular(AgregadosObra.Vacio, null).FechaInicio);
    }

    [TestMethod]
    public void EjecucionFisica_PromedioPonderadoPorMontoAutorizado_YSinDatoSinAutorizantes()
    {
        var i = IndicadoresObra.Calcular(Muestra, 2000m);
        Assert.AreEqual(20m, i.EjecFisica, "(1000·50 + 3000·10) / 4000");
        Assert.AreEqual(80m, i.FaltanteEjecFisica);

        Assert.IsNull(IndicadoresObra.Calcular(AgregadosObra.Vacio, 2000m).EjecFisica, "sin autorizantes no hay dato (el legado no cargaba la clave)");
        var pesoCero = IndicadoresObra.Calcular(Muestra with { Autorizantes = [new(0m, 50m)] }, 2000m);
        Assert.IsNull(pesoCero.EjecFisica, "con peso cero el legado saltaba la obra");
        Assert.IsNull(pesoCero.FaltanteEjecFisica);
    }

    [TestMethod]
    public void ObraSinMovimientos_TodoEnCero()
    {
        var i = IndicadoresObra.Calcular(AgregadosObra.Vacio, null);
        Assert.AreEqual(0m, i.MontoCertificado);
        Assert.AreEqual(0m, i.MontoInicial);
        Assert.AreEqual(0m, i.MontoActual);
        Assert.AreEqual(0m, i.MontoFaltante);
        Assert.IsNull(i.Porcentaje);
        Assert.IsNull(i.EjecFisica);
    }

    [TestMethod]
    public void ObraValidator_ExigeTodosLosCamposDelFormularioDelLegado()
    {
        var errores = ObraValidator.Validar(new ObraViewModel());
        Assert.AreEqual(9, errores.Count, string.Join(" | ", errores));

        var ok = new ObraViewModel
        {
            Descripcion = "TORRE A", AreaId = 1, EmpresaId = 2, ContrataId = 3, BarrioId = 4,
            Numero = 12, Anio = 2024, Etapa = 1, ObraNumero = 7,
        };
        Assert.AreEqual(0, ObraValidator.Validar(ok).Count);

        var anioRaro = ObraValidator.Validar(new ObraViewModel
        {
            Descripcion = "X", AreaId = 1, EmpresaId = 2, ContrataId = 3, BarrioId = 4, Numero = 1, Anio = 24, Etapa = 1, ObraNumero = 1,
        });
        Assert.IsTrue(anioRaro.Any(e => e.Contains("1900")), string.Join(" | ", anioRaro));
    }

    [TestMethod]
    public void ReglasPermisos_VeTodasLasObras_AdminYRedeterminaciones()
    {
        static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
            true, "u", "U", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

        Assert.IsTrue(ReglasPermisosIvc.VeTodasLasObras(Usuario(admin: true)));
        Assert.IsTrue(ReglasPermisosIvc.VeTodasLasObras(Usuario(admin: false, IvcAreaIds.Redeterminaciones)));
        Assert.IsFalse(ReglasPermisosIvc.VeTodasLasObras(Usuario(admin: false, 26)));
        Assert.IsFalse(ReglasPermisosIvc.VeTodasLasObras(Usuario(admin: false)));
        Assert.IsFalse(ReglasPermisosIvc.VeTodasLasObras(UsuarioIvc.Anonimo));
    }
}
