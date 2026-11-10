using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

/// <summary>
/// Reglas de fila de los catálogos: obligatorios, largos de columna y la regla de montos
/// del legado ("sólo números positivos con hasta 2 decimales").
/// </summary>
[TestClass]
public class CatalogosValidatorsTests
{
    [TestMethod]
    public void CatalogoSimple_NombreObligatorio_YLargoDeColumna()
    {
        CollectionAssert.Contains(CatalogoSimpleValidator.Validar(new() { Nombre = "  " }).ToList(), "El nombre es obligatorio.");
        Assert.AreEqual(0, CatalogoSimpleValidator.Validar(new() { Nombre = "Villa Lugano" }).Count);
        var largo = CatalogoSimpleValidator.Validar(new() { Nombre = new string('x', 256) });
        Assert.AreEqual(1, largo.Count);
        StringAssert.Contains(largo[0], "255");
    }

    [TestMethod]
    public void LineaGestion_SoloElNombreEsObligatorio_YTodosLosTextosTienenTope()
    {
        Assert.AreEqual(0, LineaGestionValidator.Validar(new() { Nombre = "Vivienda" }).Count);
        var errores = LineaGestionValidator.Validar(new()
        {
            Nombre = string.Empty,
            Tipo = new string('t', 256),
            Grupo = new string('g', 256),
            Reparticion = new string('r', 256),
        });
        Assert.AreEqual(4, errores.Count, string.Join(" | ", errores));
    }

    [TestMethod]
    public void LineaGestionFF_ExigeLineaYNombre()
    {
        var errores = LineaGestionFFValidator.Validar(new() { LineaGestionId = 0, Nombre = string.Empty });
        CollectionAssert.AreEquivalent(new[] { "Elegí la línea de gestión.", "El nombre FF es obligatorio." }, errores.ToList());
        Assert.AreEqual(0, LineaGestionFFValidator.Validar(new() { LineaGestionId = 3, Nombre = "FF 11" }).Count);
    }

    [TestMethod]
    public void Proyecto_EnElAltaExigeObra_YSiempreProyectoLineaYMonto()
    {
        var vacio = new ProyectoViewModel();
        var alta = ProyectoValidator.Validar(vacio, esAlta: true);
        CollectionAssert.Contains(alta.ToList(), "Elegí la obra.");
        CollectionAssert.Contains(alta.ToList(), "El proyecto es obligatorio.");
        CollectionAssert.Contains(alta.ToList(), "Elegí la línea de gestión.");
        CollectionAssert.Contains(alta.ToList(), "El monto autorizado 2026 es obligatorio.");

        var edicion = ProyectoValidator.Validar(vacio, esAlta: false);
        Assert.IsFalse(edicion.Contains("Elegí la obra."), "en la edición la obra ya está fija");

        var ok = new ProyectoViewModel { ObraId = 1, Nombre = "P", LineaGestionId = 2, Autorizado2026 = 1000.50m };
        Assert.AreEqual(0, ProyectoValidator.Validar(ok, esAlta: true).Count);
    }

    [TestMethod]
    public void Montos_ComoElLegado_PositivosConHastaDosDecimales()
    {
        var negativo = ProyectoValidator.Validar(new() { ObraId = 1, Nombre = "P", LineaGestionId = 2, Autorizado2026 = -1m }, esAlta: true);
        Assert.IsTrue(negativo.Any(e => e.Contains("negativo")), string.Join(" | ", negativo));

        var tresDecimales = MovimientoValidator.Validar(new() { ObraId = 1, Monto = 10.123m, Fecha = DateTime.Today }, esAlta: true);
        Assert.IsTrue(tresDecimales.Any(e => e.Contains("2 decimales")), string.Join(" | ", tresDecimales));

        Assert.AreEqual(0, MovimientoValidator.Validar(new() { ObraId = 1, Monto = 10.12m, Fecha = DateTime.Today }, esAlta: true).Count);
    }

    [TestMethod]
    public void Movimiento_ExigeObraEnElAlta_MontoYFecha()
    {
        var errores = MovimientoValidator.Validar(new(), esAlta: true);
        CollectionAssert.AreEquivalent(
            new[] { "Elegí la obra.", "El monto del movimiento es obligatorio.", "La fecha es obligatoria." },
            errores.ToList());
        Assert.AreEqual(2, MovimientoValidator.Validar(new(), esAlta: false).Count, "sin la obra en la edición");
    }
}
