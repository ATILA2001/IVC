#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Usuario del legado (tabla Usuarios, anterior al SSO). Sólo lectura: la app nueva lo
/// necesita para mostrar quién cargó cada redeterminación (Redeterminaciones.ID_USUARIO)
/// y para el combo de usuarios de esa grilla. La columna Contrasenia no se mapea.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Correo { get; set; }
    /// <summary>true: administrador.</summary>
    public bool? Tipo { get; set; }
    /// <summary>true: activo.</summary>
    public bool? Estado { get; set; }
    /// <summary>Columna AREA.</summary>
    public int? AreaId { get; set; }
    public Area? Area { get; set; }
}
