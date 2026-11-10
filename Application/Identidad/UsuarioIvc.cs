#nullable enable
namespace IVC.Application.Identidad;

/// <summary>
/// El usuario actual visto por IVC: lo que viene en la cookie de Auth.Web (identidad,
/// roles, áreas) más lo que IVC le agrega desde su base (áreas IVC resueltas por
/// AREAS.AuthAreaId y el override de planificación de UsuariosVinculados).
/// Inmutable: se construye una vez por circuito (ver IUsuarioActual) y las reglas de
/// <see cref="ReglasPermisosIvc"/> lo reciben como entrada.
/// </summary>
public sealed record UsuarioIvc(
    bool Autenticado,
    string AuthUserId,
    string Nombre,
    string? Correo,
    bool EsAdmin,
    IReadOnlyList<int> AuthAreaIds,
    IReadOnlyList<int> IvcAreaIds,
    int? AreaPrincipalId,
    string? AreaPrincipalNombre,
    bool PlanificacionAbiertaOverride)
{
    /// <summary>Sin sesión autenticada: ninguna regla le concede nada.</summary>
    public static readonly UsuarioIvc Anonimo = new(
        Autenticado: false, AuthUserId: string.Empty, Nombre: string.Empty, Correo: null, EsAdmin: false,
        AuthAreaIds: Array.Empty<int>(), IvcAreaIds: Array.Empty<int>(),
        AreaPrincipalId: null, AreaPrincipalNombre: null, PlanificacionAbiertaOverride: false);
}

/// <summary>Interruptores globales de la tabla ABM_PLANI.</summary>
public sealed record EstadoPlanificacion(bool PlanificacionAbierta, bool FormulacionAbierta)
{
    public static readonly EstadoPlanificacion Cerrado = new(false, false);
}
