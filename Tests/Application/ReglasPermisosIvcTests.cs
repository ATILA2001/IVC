using IVC.Application.Identidad;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Application;

/// <summary>
/// Reglas de edición portadas de UserHelper del legado (UserHelperTests,
/// UserHelperEdicionInlineTests) más las de área que allí no tenían test.
/// </summary>
[TestClass]
public class ReglasPermisosIvcTests
{
    private static readonly EstadoPlanificacion Abierto = new(PlanificacionAbierta: true, FormulacionAbierta: true);
    private static readonly EstadoPlanificacion Cerrado = EstadoPlanificacion.Cerrado;

    private static UsuarioIvc Usuario(bool admin = false, bool overridePlani = false, params int[] areas) => new(
        Autenticado: true, AuthUserId: "u1", Nombre: "Ana", Correo: null, EsAdmin: admin,
        AuthAreaIds: Array.Empty<int>(), IvcAreaIds: areas, AreaPrincipalId: areas.FirstOrDefault(),
        AreaPrincipalNombre: null, PlanificacionAbiertaOverride: overridePlani);

    // ---- AreaIdsParaFiltro (UserHelperTests) ----

    [TestMethod]
    public void AreaIdsParaFiltro_UsuarioNull_DevuelveListaVacia()
    {
        var ids = ReglasPermisosIvc.AreaIdsParaFiltro(null);
        Assert.IsNotNull(ids);
        Assert.AreEqual(0, ids.Count);
    }

    [TestMethod]
    public void AreaIdsParaFiltro_SinAreasResueltas_DevuelveListaVacia()
    {
        var ids = ReglasPermisosIvc.AreaIdsParaFiltro(Usuario());
        Assert.AreEqual(0, ids.Count, "Un usuario sin áreas filtra con lista vacía (fail-closed), nunca con null");
    }

    [TestMethod]
    public void AreaIdsParaFiltro_ConAreas_DevuelveLasMismas()
    {
        var ids = ReglasPermisosIvc.AreaIdsParaFiltro(Usuario(areas: [16, 19]));
        CollectionAssert.AreEqual(new[] { 16, 19 }, ids.ToArray());
    }

    // ---- EsDeArea ----

    [TestMethod]
    public void EsDeArea_AdminNoEsDeNingunArea_YAnonimoTampoco()
    {
        Assert.IsFalse(ReglasPermisosIvc.EsDeArea(Usuario(admin: true, areas: [IvcAreaIds.Secretaria]), IvcAreaIds.Secretaria));
        Assert.IsFalse(ReglasPermisosIvc.EsDeArea(UsuarioIvc.Anonimo, IvcAreaIds.Secretaria));
        Assert.IsTrue(ReglasPermisosIvc.EsDeArea(Usuario(areas: [18, 19, 20]), IvcAreaIds.Secretaria));
    }

    // ---- PuedeEditarInline (UserHelperEdicionInlineTests, PR #17) ----

    [TestMethod]
    public void PuedeEditarInline_Anonimo_No()
        => Assert.IsFalse(ReglasPermisosIvc.PuedeEditarInline(UsuarioIvc.Anonimo));

    [TestMethod]
    public void PuedeEditarInline_UsuarioNormal_SiAunqueLaPlanificacionEsteCerradaYSeaDeCualquierArea()
    {
        Assert.IsTrue(ReglasPermisosIvc.PuedeEditarInline(Usuario()));
        Assert.IsTrue(ReglasPermisosIvc.PuedeEditarInline(Usuario(areas: [IvcAreaIds.Redeterminaciones])));
        Assert.IsTrue(ReglasPermisosIvc.PuedeEditarInline(Usuario(areas: [IvcAreaIds.Secretaria])));
    }

    [TestMethod]
    public void PuedeEditarInline_Administrador_Si()
        => Assert.IsTrue(ReglasPermisosIvc.PuedeEditarInline(Usuario(admin: true)));

    // ---- PuedeModificarPlanificacion ----

    [TestMethod]
    public void PuedeModificarPlanificacion_Admin_SiempreAunqueEsteCerrada()
        => Assert.IsTrue(ReglasPermisosIvc.PuedeModificarPlanificacion(Usuario(admin: true), Cerrado));

    [TestMethod]
    public void PuedeModificarPlanificacion_UsuarioNormal_SoloConPlanificacionAbiertaOConOverride()
    {
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarPlanificacion(Usuario(), Cerrado));
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarPlanificacion(Usuario(), Abierto));
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarPlanificacion(Usuario(overridePlani: true), Cerrado));
    }

    [TestMethod]
    public void PuedeModificarPlanificacion_RedeterminacionesYSecretaria_NuncaAunqueEsteAbierta()
    {
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarPlanificacion(Usuario(areas: [IvcAreaIds.Redeterminaciones]), Abierto));
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarPlanificacion(Usuario(overridePlani: true, areas: [IvcAreaIds.Secretaria]), Abierto));
    }

    [TestMethod]
    public void PuedeModificarPlanificacion_Anonimo_No()
        => Assert.IsFalse(ReglasPermisosIvc.PuedeModificarPlanificacion(UsuarioIvc.Anonimo, Abierto));

    // ---- PuedeModificarFormulacion ----

    [TestMethod]
    public void PuedeModificarFormulacion_Admin_Siempre_UsuarioNormal_SoloAbierta_AreasRestringidas_Nunca()
    {
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarFormulacion(Usuario(admin: true), Cerrado));
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarFormulacion(Usuario(), Cerrado));
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarFormulacion(Usuario(), Abierto));
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarFormulacion(Usuario(areas: [IvcAreaIds.Redeterminaciones]), Abierto));
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarFormulacion(Usuario(areas: [IvcAreaIds.Secretaria]), Abierto));
        // El override es sólo de planificación: no abre la formulación.
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarFormulacion(Usuario(overridePlani: true), Cerrado));
    }

    // ---- PuedeModificarRedeterminaciones ----

    [TestMethod]
    public void PuedeModificarRedeterminaciones_TodosSalvoSecretariaYAnonimo()
    {
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarRedeterminaciones(Usuario(admin: true)));
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarRedeterminaciones(Usuario()));
        Assert.IsTrue(ReglasPermisosIvc.PuedeModificarRedeterminaciones(Usuario(areas: [IvcAreaIds.Redeterminaciones])));
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarRedeterminaciones(Usuario(areas: [IvcAreaIds.Secretaria])));
        Assert.IsFalse(ReglasPermisosIvc.PuedeModificarRedeterminaciones(UsuarioIvc.Anonimo));
    }

    // ---- VeColumnasFinancieras ----

    [TestMethod]
    public void VeColumnasFinancieras_OcultasSoloARedeterminaciones()
    {
        Assert.IsTrue(ReglasPermisosIvc.VeColumnasFinancieras(Usuario()));
        Assert.IsTrue(ReglasPermisosIvc.VeColumnasFinancieras(Usuario(admin: true, areas: [IvcAreaIds.Redeterminaciones])));
        Assert.IsFalse(ReglasPermisosIvc.VeColumnasFinancieras(Usuario(areas: [IvcAreaIds.Redeterminaciones])));
        Assert.IsFalse(ReglasPermisosIvc.VeColumnasFinancieras(UsuarioIvc.Anonimo));
    }
}
