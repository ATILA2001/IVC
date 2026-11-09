#nullable enable
namespace IVC.Data.Entities;

/// <summary>Fuente de financiamiento de una línea de gestión. Tabla LINEA_DE_GESTION_FF.</summary>
public class LineaGestionFF
{
    public int Id { get; set; }
    /// <summary>Columna ID_LINEA_GESTION.</summary>
    public int LineaGestionId { get; set; }
    public LineaGestion LineaGestion { get; set; } = null!;
    /// <summary>Columna NOMBRE_FF.</summary>
    public string? Nombre { get; set; }
    public string? Fuente { get; set; }
}
