using IVC.Application.Formulacion;
using IVC.Application.Formulacion.Dtos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

/// <summary>
/// El ciclo de formulación (ex Negocio.FormulacionCiclo) y las reglas del formulario del
/// legado: al menos un monto, montos positivos con dos decimales, catálogos obligatorios y
/// largos de las columnas de texto.
/// </summary>
[TestClass]
public class FormulacionTests
{
    [TestMethod]
    public void Ciclo_TresAniosDesdeElBase_YPosicionPorAnio()
    {
        var ciclo = new FormulacionCiclo(2027);
        CollectionAssert.AreEqual(new[] { 2027, 2028, 2029 }, ciclo.Anios);
        Assert.AreEqual(0, ciclo.Posicion(2027));
        Assert.AreEqual(2, ciclo.Posicion(2029));
        Assert.IsNull(ciclo.Posicion(2026), "el año anterior queda fuera de la ventana");
        Assert.IsNull(ciclo.Posicion(2030));
        Assert.IsTrue(ciclo.Contiene(2028));
        Assert.AreEqual(new DateTime(2028, 1, 1), FormulacionCiclo.FechaPeriodo(2028), "FECHA_PERIODO es el 1 de enero");
    }

    [TestMethod]
    public void Ciclo_DesdeOpciones_UsaElAnioConfigurado_OElPorDefecto()
    {
        Assert.AreEqual(2030, FormulacionCiclo.Desde(new FormulacionOptions { AnioBase = 2030 }).AnioBase);
        Assert.AreEqual(FormulacionOptions.AnioBasePorDefecto, FormulacionCiclo.Desde(null).AnioBase);
        Assert.AreEqual(FormulacionOptions.AnioBasePorDefecto, FormulacionCiclo.Desde(new FormulacionOptions { AnioBase = 0 }).AnioBase, "un valor inválido cae al 2027 del legado");
    }

    private static FormulacionViewModel Valida() => new()
    {
        ObraId = 10, MontoAnio1 = 100m, UnidadMedidaId = 1, ValorMedida = 5m, PrioridadId = 2,
    };

    [TestMethod]
    public void Validar_ExigeAlMenosUnMonto_YCatalogosObligatorios()
    {
        Assert.AreEqual(0, FormulacionValidator.Validar(Valida(), esAlta: true).Count);

        var sinMontos = Valida();
        sinMontos.MontoAnio1 = null;
        StringAssert.Contains(string.Join(" ", FormulacionValidator.Validar(sinMontos, esAlta: true)), "al menos un monto");

        var sinCatalogos = Valida();
        sinCatalogos.UnidadMedidaId = null;
        sinCatalogos.PrioridadId = 0;
        sinCatalogos.ValorMedida = null;
        Assert.AreEqual(3, FormulacionValidator.Validar(sinCatalogos, esAlta: true).Count);

        var sinObra = Valida();
        sinObra.ObraId = 0;
        Assert.AreEqual(1, FormulacionValidator.Validar(sinObra, esAlta: true).Count, "la obra sólo se exige en el alta");
        Assert.AreEqual(0, FormulacionValidator.Validar(sinObra, esAlta: false).Count);
    }

    [TestMethod]
    public void Validar_MontosPositivosConDosDecimales_YLargosDeTexto()
    {
        var vm = Valida();
        vm.MontoAnio2 = -1m;
        vm.MontoAnio3 = 1.234m;
        vm.Techos = -5m;
        vm.Ppi = -1;
        Assert.AreEqual(4, FormulacionValidator.Validar(vm, esAlta: true).Count);
        Assert.AreEqual(2, FormulacionValidator.Validar(vm, esAlta: true, editaPpiYTecho: false).Count,
            "quien no edita PPI ni techo no queda trabado por un valor heredado");

        var largo = Valida();
        largo.BreveDescripcion = new string('X', FormulacionValidator.BreveDescripcionMaxLength + 1);
        largo.Observaciones = new string('X', FormulacionValidator.ObservacionesMaxLength + 1);
        var errores = FormulacionValidator.Validar(largo, esAlta: true);
        Assert.AreEqual(2, errores.Count);
        StringAssert.Contains(errores[0], "500");
        StringAssert.Contains(errores[1], "255");
    }

    [TestMethod]
    public void ViewModel_TotalYMontosPosicionales()
    {
        var vm = new FormulacionViewModel { MontoAnio1 = 10m, MontoAnio3 = 5.5m };
        Assert.AreEqual(15.5m, vm.TotalMonto, "los nulos cuentan como cero");
        CollectionAssert.AreEqual(new decimal?[] { 10m, null, 5.5m }, vm.Montos);
    }

    [TestMethod]
    public void ExportSinPpi_CopiaTodoMenosElPpi_YConservaLosNombresDeMonto()
    {
        var vm = new FormulacionViewModel { ObraId = 1, Obra = "TORRE", Ppi = 7, Techos = 900m, MontoAnio1 = 1m, MontoAnio3 = 3m, Prioridad = "ALTA" };
        var fila = FormulacionExportSinPpi.De(vm);
        Assert.AreEqual("TORRE", fila.Obra);
        Assert.AreEqual(900m, fila.Techos);
        Assert.AreEqual(3m, fila.MontoAnio3);
        Assert.AreEqual("ALTA", fila.Prioridad);
        var nombres = typeof(FormulacionExportSinPpi).GetProperties().Select(p => p.Name).ToList();
        Assert.IsFalse(nombres.Contains(nameof(FormulacionViewModel.Ppi)), "sin PPI");
        CollectionAssert.IsSubsetOf(new[] { "MontoAnio1", "MontoAnio2", "MontoAnio3" }, nombres, "los títulos por año de la página aplican");
    }
}
