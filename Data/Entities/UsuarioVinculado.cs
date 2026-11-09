#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Atributos IVC de un usuario de Auth.Web. Tabla UsuariosVinculados, clave AuthUserId
/// (el NameIdentifier de la cookie). Se da de alta sola la primera vez que el usuario entra.
/// </summary>
public class UsuarioVinculado
{
    public string AuthUserId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Override por usuario: puede modificar planificación aunque esté cerrada (PLANIFICACION_ABIERTA).</summary>
    public bool PlanificacionAbierta { get; set; }
}
