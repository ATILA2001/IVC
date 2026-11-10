#nullable enable
namespace IVC.Data.Entities;

/// <summary>Línea de gestión presupuestaria. Tabla Linea_de_Gestion.</summary>
public class LineaGestion
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Tipo { get; set; }
    public string? Grupo { get; set; }
    public string? Reparticion { get; set; }
    public ICollection<LineaGestionFF> FuentesFinanciamiento { get; set; } = new List<LineaGestionFF>();
}
