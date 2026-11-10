#nullable enable
namespace IVC.Application.Identidad;

/// <summary>
/// Ids de las áreas del IVC con reglas propias (columna ID de la tabla AREAS). El mapeo
/// desde las áreas de Auth.Web se configura en AREAS.AuthAreaId: las tres áreas de
/// Secretaría (18, 19 y 20) comparten el AuthAreaId 36, por eso un usuario de Secretaría
/// resuelve a las tres y la regla pregunta por la 19.
/// </summary>
public static class IvcAreaIds
{
    /// <summary>Redeterminaciones: ve Obras, Autorizantes y Redeterminaciones; no planifica ni formula.</summary>
    public const int Redeterminaciones = 16;

    /// <summary>Secretaría: sólo lectura en planificación, formulación y redeterminaciones.</summary>
    public const int Secretaria = 19;
}
