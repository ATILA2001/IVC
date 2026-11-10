#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Proyecto presupuestario de una obra. Tabla BD_PROYECTOS (una fila por obra como máximo).
/// El legado la mapeaba con la clave en ID_BASE y sin ID; acá la clave es ID y ObraId es única.
/// </summary>
public class Proyecto
{
    public int Id { get; set; }
    /// <summary>Obra (columna ID_BASE).</summary>
    public int ObraId { get; set; }
    public Obra Obra { get; set; } = null!;
    /// <summary>Nombre del proyecto (columna PROYECTO).</summary>
    public string? Nombre { get; set; }
    /// <summary>Columna SUBPROYECTO.</summary>
    public string SubProyecto { get; set; } = string.Empty;
    /// <summary>Columna LINEA_DE_GESTION.</summary>
    public int? LineaGestionId { get; set; }
    public LineaGestion? LineaGestion { get; set; }
    public decimal? Autorizado2025 { get; set; }
    public decimal? Autorizado2026 { get; set; }
}
