#nullable enable
namespace IVC.Application.Identidad;

/// <summary>
/// Reglas funcionales de edición del IVC (las que perms_json no expresa): dependen del
/// rol, del área y de los interruptores de ABM_PLANI. Puras: reciben el usuario y el
/// estado y no tocan sesión ni base. Portadas de UserHelper del legado, incluido el
/// dictamen del área del 04/11/2026 (PR #17) sobre los campos inline.
/// Se verifican en cada handler que escribe, además de ocultar controles en la UI.
/// </summary>
public static class ReglasPermisosIvc
{
    /// <summary>
    /// Pertenencia a un área IVC. Un administrador no "es" de ningún área: sus permisos
    /// vienen del rol, y las restricciones por área no lo alcanzan.
    /// </summary>
    public static bool EsDeArea(UsuarioIvc usuario, int ivcAreaId)
        => usuario.Autenticado && !usuario.EsAdmin && usuario.IvcAreaIds.Contains(ivcAreaId);

    /// <summary>
    /// Áreas con las que se filtran las grillas de planificación. Nunca null: un usuario
    /// sin áreas resueltas filtra con lista vacía y no ve nada (fail-closed).
    /// </summary>
    public static IReadOnlyList<int> AreaIdsParaFiltro(UsuarioIvc? usuario)
        => usuario?.IvcAreaIds ?? Array.Empty<int>();

    /// <summary>
    /// Alta, modificación por ventana y baja en Obras, Autorizantes, Certificados y
    /// Legítimos: administrador siempre; el resto sólo con la planificación abierta o
    /// con override propio. Redeterminaciones y Secretaría nunca.
    /// </summary>
    public static bool PuedeModificarPlanificacion(UsuarioIvc usuario, EstadoPlanificacion estado)
    {
        if (!usuario.Autenticado) return false;
        if (EsDeArea(usuario, IvcAreaIds.Redeterminaciones) || EsDeArea(usuario, IvcAreaIds.Secretaria)) return false;
        return usuario.EsAdmin || estado.PlanificacionAbierta || usuario.PlanificacionAbiertaOverride;
    }

    /// <summary>
    /// Campos inline de las grillas: expediente, monto y ejecución física en Certificados;
    /// expediente y certificado en Legítimos; expediente y estado en Autorizantes.
    /// Cualquier usuario autenticado con acceso a la página: no dependen de la
    /// planificación ni del área (dictamen del área del 04/11/2026).
    /// </summary>
    public static bool PuedeEditarInline(UsuarioIvc usuario) => usuario.Autenticado;

    /// <summary>Formulación: administrador siempre; Redeterminaciones y Secretaría nunca; el resto con la formulación abierta.</summary>
    public static bool PuedeModificarFormulacion(UsuarioIvc usuario, EstadoPlanificacion estado)
    {
        if (!usuario.Autenticado) return false;
        if (usuario.EsAdmin) return true;
        if (EsDeArea(usuario, IvcAreaIds.Redeterminaciones) || EsDeArea(usuario, IvcAreaIds.Secretaria)) return false;
        return estado.FormulacionAbierta;
    }

    /// <summary>Redeterminaciones: todos los autenticados salvo Secretaría.</summary>
    public static bool PuedeModificarRedeterminaciones(UsuarioIvc usuario)
    {
        if (!usuario.Autenticado) return false;
        if (usuario.EsAdmin) return true;
        return !EsDeArea(usuario, IvcAreaIds.Secretaria);
    }

    /// <summary>Columnas financieras de Obras: ocultas al área Redeterminaciones.</summary>
    public static bool VeColumnasFinancieras(UsuarioIvc usuario)
        => usuario.Autenticado && !EsDeArea(usuario, IvcAreaIds.Redeterminaciones);

    /// <summary>
    /// Obras: administradores y el área Redeterminaciones ven todas; el resto, sólo las
    /// de sus áreas (con <see cref="AreaIdsParaFiltro"/>).
    /// </summary>
    public static bool VeTodasLasObras(UsuarioIvc usuario)
        => usuario.Autenticado && (usuario.EsAdmin || EsDeArea(usuario, IvcAreaIds.Redeterminaciones));
}
