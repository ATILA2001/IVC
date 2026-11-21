using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

/// <summary>
/// Reglas de fila de los cuatro formularios del módulo (las que exigían los validadores
/// del legado) y los días en buzón de la grilla de Redeterminaciones.
/// </summary>
[TestClass]
public class RedeterminacionesValidadoresTests
{
    private static AutorizanteViewModel AutorizanteValido() => new()
    {
        ObraId = 10, ConceptoId = 4, EstadoId = 1, Detalle = "BASICO", Expediente = "EX-1",
        MontoAutorizado = 1000m, MesAprobacion = new DateTime(2025, 1, 1),
    };

    [TestMethod]
    public void Autorizante_ExigeObraSoloEnElAlta_YMontoPositivoConDosDecimales()
    {
        Assert.AreEqual(0, AutorizanteValidator.Validar(AutorizanteValido(), esAlta: true).Count);

        var sinObra = AutorizanteValido();
        sinObra.ObraId = 0;
        StringAssert.Contains(string.Join(" ", AutorizanteValidator.Validar(sinObra, esAlta: true)), "obra");
        Assert.AreEqual(0, AutorizanteValidator.Validar(sinObra, esAlta: false).Count, "al editar la obra no se elige");

        var monto = AutorizanteValido();
        monto.MontoAutorizado = 10.123m;
        StringAssert.Contains(string.Join(" ", AutorizanteValidator.Validar(monto, esAlta: false)), "2 decimales");
        monto.MontoAutorizado = -1m;
        StringAssert.Contains(string.Join(" ", AutorizanteValidator.Validar(monto, esAlta: false)), "negativo");

        var vacio = new AutorizanteViewModel();
        var errores = AutorizanteValidator.Validar(vacio, esAlta: true);
        Assert.AreEqual(6, errores.Count, string.Join(" | ", errores));
    }

    [TestMethod]
    public void Autorizante_Seguimiento_SoloLargoDeExpedienteYEstado()
    {
        var vm = new AutorizanteViewModel { EstadoId = 1, Expediente = new string('X', 51) };
        StringAssert.Contains(string.Join(" ", AutorizanteValidator.ValidarSeguimiento(vm)), "50");
        vm.Expediente = null;
        Assert.AreEqual(0, AutorizanteValidator.ValidarSeguimiento(vm).Count, "sin expediente es válido");
        vm.EstadoId = null;
        Assert.AreEqual(1, AutorizanteValidator.ValidarSeguimiento(vm).Count);
    }

    [TestMethod]
    public void Certificado_ExigeAutorizanteEnElAlta_YElInlineNoTocaLaReliquidacion()
    {
        var vm = new CertificadoViewModel { AutorizanteId = 1, TipoPagoId = 2, MontoTotal = 100m, MesAprobacion = new DateTime(2025, 1, 1) };
        Assert.AreEqual(0, CertificadoValidator.Validar(vm, esAlta: true).Count);
        vm.AutorizanteId = null;
        Assert.AreEqual(1, CertificadoValidator.Validar(vm, esAlta: true).Count);
        Assert.AreEqual(0, CertificadoValidator.Validar(vm, esAlta: false).Count);

        var real = new CertificadoViewModel { Id = 3, TipoPagoId = 2, MontoTotal = null, PorcEjecFisica = 120m };
        var errores = CertificadoValidator.ValidarInline(real);
        Assert.AreEqual(2, errores.Count, string.Join(" | ", errores));

        var reliq = new CertificadoViewModel { Id = 0, TipoPagoId = ReglasRedeterminaciones.TipoPagoReliquidacionId, MontoTotal = null, MesAprobacion = new DateTime(2025, 7, 1) };
        Assert.IsTrue(reliq.EsReliquidacion);
        Assert.AreEqual(0, CertificadoValidator.ValidarInline(reliq).Count, "en la reliquidación sólo se edita el expediente");
    }

    [TestMethod]
    public void Legitimo_ExigeFechasCoherentes()
    {
        var vm = new LegitimoViewModel
        {
            ObraId = 10, CodigoAutorizante = "OBRA-1", Expediente = "EX-1",
            InicioEjecucion = new DateTime(2025, 3, 1), FinEjecucion = new DateTime(2025, 1, 1),
            Certificado = 10m, MesAprobacion = new DateTime(2025, 1, 1),
        };
        StringAssert.Contains(string.Join(" ", LegitimoValidator.Validar(vm)), "anterior al inicio");
        vm.FinEjecucion = new DateTime(2025, 4, 1);
        Assert.AreEqual(0, LegitimoValidator.Validar(vm).Count);

        var inline = new LegitimoViewModel { Expediente = "", Certificado = -5m };
        Assert.AreEqual(1, LegitimoValidator.ValidarInline(inline).Count);
    }

    [TestMethod]
    public void Redeterminacion_AdmitePorcentajeNegativo_PeroNoMasDeDosDecimalesNiTresEnteros()
    {
        var vm = new RedeterminacionViewModel
        {
            AutorizanteId = 1, Expediente = "EX-1", Salto = new DateTime(2025, 6, 1), Nro = 1, Tipo = "DEFINITIVA", EtapaId = 12, Porcentaje = -3.5m,
        };
        Assert.AreEqual(0, RedeterminacionValidator.Validar(vm, esAlta: true).Count, "hay porcentajes negativos en producción");
        vm.Porcentaje = 1000m;
        Assert.AreEqual(1, RedeterminacionValidator.Validar(vm, esAlta: true).Count);
        vm.Porcentaje = 1.234m;
        Assert.AreEqual(1, RedeterminacionValidator.Validar(vm, esAlta: true).Count);

        var seguimiento = new RedeterminacionViewModel { EtapaId = null };
        Assert.AreEqual(1, RedeterminacionValidator.ValidarSeguimiento(seguimiento).Count);
    }

    [TestMethod]
    public void DiasEnBuzon_SoloParaEtapasEnTramite_YSemaforoDelLegado()
    {
        var hoy = new DateTime(2026, 11, 10, 15, 0, 0);
        Assert.AreEqual(10, SeguimientoBuzon.DiasEnBuzon(hoy.AddDays(-10).AddHours(-2), etapaId: 2, hoy), "días completos (floor)");
        Assert.IsNull(SeguimientoBuzon.DiasEnBuzon(hoy.AddDays(-10), etapaId: 12, hoy), "aprobada: no se sigue");
        Assert.IsNull(SeguimientoBuzon.DiasEnBuzon(hoy.AddDays(-10), etapaId: 34, hoy));
        Assert.IsNull(SeguimientoBuzon.DiasEnBuzon(null, etapaId: 2, hoy));
        Assert.IsNull(SeguimientoBuzon.DiasEnBuzon(hoy, etapaId: null, hoy));

        Assert.AreEqual(SeguimientoBuzon.Rango.MenosDeSiete, SeguimientoBuzon.RangoDe(6));
        Assert.AreEqual(SeguimientoBuzon.Rango.DeSieteACatorce, SeguimientoBuzon.RangoDe(7));
        Assert.AreEqual(SeguimientoBuzon.Rango.DeSieteACatorce, SeguimientoBuzon.RangoDe(14));
        Assert.AreEqual(SeguimientoBuzon.Rango.MasDeCatorce, SeguimientoBuzon.RangoDe(15));
        Assert.IsTrue(SeguimientoBuzon.CumpleRango(null, SeguimientoBuzon.Rango.Todos));
        Assert.IsFalse(SeguimientoBuzon.CumpleRango(null, SeguimientoBuzon.Rango.MenosDeSiete), "sin días no entra en ningún rango");
        Assert.IsTrue(SeguimientoBuzon.CumpleRango(20, SeguimientoBuzon.Rango.MasDeCatorce));
    }
}
