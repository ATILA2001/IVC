using IVC.Data;
using IVC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Data;

/// <summary>
/// El modelo EF se construye sin errores y mapea las tablas del legado con sus nombres
/// reales. No valida tipos ni nulabilidad contra SQL Server: eso lo hace la comparación
/// manual con Database/esquema-columnas.sql sobre IVC_TEST.
/// </summary>
[TestClass]
public class IvcDbContextModelTests
{
    private static IvcDbContext Crear()
    {
        var options = new DbContextOptionsBuilder<IvcDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IvcDbContext(options);
    }

    [TestMethod]
    public void ElModeloSeConstruye_YCadaEntidadTieneClave_SalvoLasBajadasExternas()
    {
        using var db = Crear();
        var sinClave = db.Model.GetEntityTypes()
            .Where(t => t.FindPrimaryKey() is null)
            .Select(t => t.ClrType.Name)
            .OrderBy(n => n)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(Devengado), nameof(Gedo), nameof(PaseSade) }, sinClave);
    }

    [TestMethod]
    public void LasTablasDelLegadoConservanSuNombre()
    {
        using var db = Crear();
        var tablas = db.Model.GetEntityTypes()
            .ToDictionary(t => t.ClrType.Name, t => t.GetTableName());

        Assert.AreEqual("OBRAS", tablas[nameof(Obra)]);
        Assert.AreEqual("BD_PROYECTOS", tablas[nameof(Proyecto)]);
        Assert.AreEqual("MOVIMIENTOS_GESTION", tablas[nameof(MovimientoGestion)]);
        Assert.AreEqual("FORMULACION", tablas[nameof(Formulacion)]);
        Assert.AreEqual("LEGITIMOS_ABONOS", tablas[nameof(Legitimo)]);
        Assert.AreEqual("AUTORIZANTES", tablas[nameof(Autorizante)]);
        Assert.AreEqual("CERTIFICADOS", tablas[nameof(Certificado)]);
        Assert.AreEqual("REDETERMINACIONES", tablas[nameof(Redeterminacion)]);
        Assert.AreEqual("EXPEDIENTES_RELIQ", tablas[nameof(ExpedienteReliq)]);
        Assert.AreEqual("ESTADOS_AUTORIZANTES", tablas[nameof(EstadoAutorizante)]);
        Assert.AreEqual("ESTADOS_REDET", tablas[nameof(EstadoRedet)]);
        Assert.AreEqual("TIPO_PAGO", tablas[nameof(TipoPago)]);
        Assert.AreEqual("LINEA_DE_GESTION", tablas[nameof(LineaGestion)]);
        Assert.AreEqual("LINEA_DE_GESTION_FF", tablas[nameof(LineaGestionFF)]);
        Assert.AreEqual("ABM_PLANI", tablas[nameof(AbmPlani)]);
        Assert.AreEqual("UsuariosVinculados", tablas[nameof(UsuarioVinculado)]);
        Assert.AreEqual("PASES_SADE", tablas[nameof(PaseSade)]);
        Assert.AreEqual("GEDO", tablas[nameof(Gedo)]);
        Assert.AreEqual("DEVENGADOS", tablas[nameof(Devengado)]);
        Assert.AreEqual("CambiosAuditoria", tablas[nameof(CambioAuditoria)]);
    }

    [TestMethod]
    public void LasColumnasConNombreDistintoALaPropiedadQuedanMapeadas()
    {
        using var db = Crear();

        Assert.AreEqual("AÑO", Columna<Obra>(db, nameof(Obra.Anio)));
        Assert.AreEqual("OBRA", Columna<Obra>(db, nameof(Obra.ObraNumero)));
        Assert.AreEqual("MES", Columna<Autorizante>(db, nameof(Autorizante.MesAprobacion)));
        Assert.AreEqual("ETAPA", Columna<Redeterminacion>(db, nameof(Redeterminacion.EtapaId)));
        Assert.AreEqual("PORCENTAJE_PONDERACION", Columna<Redeterminacion>(db, nameof(Redeterminacion.Porcentaje)));
        Assert.AreEqual("ID_BASE", Columna<Proyecto>(db, nameof(Proyecto.ObraId)));
        Assert.AreEqual("MOVIMIENTO", Columna<MovimientoGestion>(db, nameof(MovimientoGestion.Monto)));
        Assert.AreEqual("BUZON DESTINO", Columna<PaseSade>(db, nameof(PaseSade.BuzonDestino)));
        Assert.AreEqual("FECHA ULTIMO PASE", Columna<PaseSade>(db, nameof(PaseSade.FechaUltimoPase)));
        Assert.AreEqual("EE_FINANCIERA", Columna<Devengado>(db, nameof(Devengado.EeFinanciera)));
    }

    [TestMethod]
    public void CodigoRedet_EsCalculadoPorLaBase_YNoSeEnviaEnInsertNiUpdate()
    {
        using var db = Crear();
        var prop = db.Model.FindEntityType(typeof(Redeterminacion))!.FindProperty(nameof(Redeterminacion.CodigoRedet))!;

        Assert.AreEqual(ValueGenerated.OnAddOrUpdate, prop.ValueGenerated);
    }

    [TestMethod]
    public void LasCadenasSonVarcharPorConvencion_YLasNvarcharLoDeclaran()
    {
        using var db = Crear();

        Assert.IsFalse(Unicode<PaseSade>(db, nameof(PaseSade.Expediente)));
        Assert.IsFalse(Unicode<Autorizante>(db, nameof(Autorizante.CodigoAutorizante)));
        Assert.IsFalse(Unicode<Obra>(db, nameof(Obra.Descripcion)));
        Assert.IsTrue(Unicode<Formulacion>(db, nameof(Formulacion.BreveDescripcion)));
        Assert.IsTrue(Unicode<UsuarioVinculado>(db, nameof(UsuarioVinculado.Nombre)));
        Assert.IsTrue(Unicode<Gedo>(db, nameof(Gedo.Expediente)));
        Assert.IsTrue(Unicode<CambioAuditoria>(db, nameof(CambioAuditoria.ClaveNegocio)));
    }

    [TestMethod]
    public void PrecisionDeLosImportes_CoincideConLaBase()
    {
        using var db = Crear();

        Assert.AreEqual((15, 2), Precision<Autorizante>(db, nameof(Autorizante.MontoAutorizado)));
        Assert.AreEqual((15, 2), Precision<Certificado>(db, nameof(Certificado.MontoTotal)));
        Assert.AreEqual((5, 2), Precision<Certificado>(db, nameof(Certificado.PorcEjecFisica)));
        Assert.AreEqual((5, 2), Precision<Redeterminacion>(db, nameof(Redeterminacion.Porcentaje)));
        Assert.AreEqual((18, 2), Precision<Formulacion>(db, nameof(Formulacion.Monto)));
        Assert.AreEqual((15, 2), Precision<MovimientoGestion>(db, nameof(MovimientoGestion.Monto)));
    }

    [TestMethod]
    public void LasClavesUnicasDeLaBase_EstanDeclaradas()
    {
        using var db = Crear();

        Assert.IsTrue(IndiceUnico<Autorizante>(db, nameof(Autorizante.CodigoAutorizante)));
        Assert.IsTrue(IndiceUnico<Proyecto>(db, nameof(Proyecto.ObraId)));
        Assert.IsTrue(IndiceUnico<ExpedienteReliq>(db, nameof(ExpedienteReliq.CodigoRedet), nameof(ExpedienteReliq.MesAprobacion)));
    }

    private static bool? Unicode<T>(IvcDbContext db, string propiedad) =>
        db.Model.FindEntityType(typeof(T))!.FindProperty(propiedad)!.IsUnicode();

    private static (int?, int?) Precision<T>(IvcDbContext db, string propiedad)
    {
        var p = db.Model.FindEntityType(typeof(T))!.FindProperty(propiedad)!;
        return (p.GetPrecision(), p.GetScale());
    }

    private static bool IndiceUnico<T>(IvcDbContext db, params string[] propiedades) =>
        db.Model.FindEntityType(typeof(T))!.GetIndexes().Any(i => i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(propiedades));

    private static string? Columna<T>(IvcDbContext db, string propiedad)
    {
        var tipo = db.Model.FindEntityType(typeof(T))!;
        var prop = tipo.FindProperty(propiedad)!;
        return prop.GetColumnName(StoreObjectIdentifier.Table(tipo.GetTableName()!, tipo.GetSchema()));
    }
}
