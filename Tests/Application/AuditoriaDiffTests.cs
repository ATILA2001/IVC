using System.ComponentModel.DataAnnotations;
using IVC.Application.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

[TestClass]
public class AuditoriaDiffTests
{
    private sealed class FilaDePrueba
    {
        [AuditIgnore] public int Id { get; set; }
        [Display(Name = "Descripción")] public string? Descripcion { get; set; }
        public decimal? Monto { get; set; }
        public DateTime? Fecha { get; set; }
        public bool Activo { get; set; }
        public List<string> Detalles { get; set; } = new();
    }

    [TestMethod]
    public void Comparar_DevuelveSoloLosCamposQueCambiaron_ConNombreVisibleYFormatoEsAr()
    {
        var antes = new FilaDePrueba { Id = 1, Descripcion = "Obra A", Monto = 1000.5m, Fecha = new DateTime(2026, 11, 3), Activo = true };
        var despues = new FilaDePrueba { Id = 1, Descripcion = "Obra B", Monto = 1000.5m, Fecha = new DateTime(2026, 11, 3), Activo = false };

        var cambios = AuditoriaDiff.Comparar(antes, despues);

        Assert.AreEqual(2, cambios.Count);
        var descripcion = cambios.Single(c => c.Campo == "Descripción");
        Assert.AreEqual("Obra A", descripcion.Antes);
        Assert.AreEqual("Obra B", descripcion.Despues);
        var activo = cambios.Single(c => c.Campo == "Activo");
        Assert.AreEqual("Sí", activo.Antes);
        Assert.AreEqual("No", activo.Despues);
    }

    [TestMethod]
    public void Comparar_IgnoraIdMarcadoYColecciones_YTrataVacioComoNull()
    {
        var antes = new FilaDePrueba { Id = 1, Descripcion = "", Detalles = new() { "x" } };
        var despues = new FilaDePrueba { Id = 2, Descripcion = null, Detalles = new() { "y", "z" } };

        var cambios = AuditoriaDiff.Comparar(antes, despues);

        Assert.AreEqual(0, cambios.Count);
    }

    [TestMethod]
    public void Snapshot_Alta_PoneLosValoresEnDespues_YOmiteLosVacios()
    {
        var item = new FilaDePrueba { Monto = 1234.5m, Descripcion = null };

        var cambios = AuditoriaDiff.Snapshot(item, esBaja: false);

        var monto = cambios.Single(c => c.Campo == "Monto");
        Assert.IsNull(monto.Antes);
        Assert.AreEqual("1.234,50", monto.Despues);
        Assert.IsFalse(cambios.Any(c => c.Campo == "Descripción"));
    }

    [TestMethod]
    public void Snapshot_Baja_PoneLosValoresEnAntes()
    {
        var item = new FilaDePrueba { Fecha = new DateTime(2026, 11, 3, 15, 30, 0) };

        var cambios = AuditoriaDiff.Snapshot(item, esBaja: true);

        var fecha = cambios.Single(c => c.Campo == "Fecha");
        Assert.AreEqual("03/11/2026 15:30", fecha.Antes);
        Assert.IsNull(fecha.Despues);
    }
}
