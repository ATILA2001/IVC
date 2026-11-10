#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Autorizante de gasto de una obra. Tabla Autorizantes. Certificados y Redeterminaciones
/// se relacionan por <see cref="CodigoAutorizante"/> (clave única, no primaria), así que
/// esa relación se resuelve en los servicios y no como navegación de EF.
/// </summary>
public class Autorizante
{
    public int Id { get; set; }
    public string? CodigoAutorizante { get; set; }
    public string? Detalle { get; set; }
    public string? Expediente { get; set; }
    public decimal? MontoAutorizado { get; set; }
    /// <summary>Mes de aprobación (columna MES).</summary>
    public DateTime? MesAprobacion { get; set; }
    public DateTime? MesBase { get; set; }
    /// <summary>Columna AUTORIZACION_GG (bit NOT NULL); el legado no la mapeaba.</summary>
    public bool AutorizacionGG { get; set; }

    // Las tres FK admiten NULL en la base aunque el legado las trataba como obligatorias.
    public int? ObraId { get; set; }
    public Obra? Obra { get; set; }
    public int? ConceptoId { get; set; }
    public Concepto? Concepto { get; set; }
    public int? EstadoId { get; set; }
    public EstadoAutorizante? Estado { get; set; }
}
