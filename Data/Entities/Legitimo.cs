#nullable enable
namespace IVC.Data.Entities;

/// <summary>Legítimo abono de una obra. Tabla LEGITIMOS_ABONOS.</summary>
public class Legitimo
{
    public int Id { get; set; }
    public int? ObraId { get; set; }
    public Obra? Obra { get; set; }
    public string CodigoAutorizante { get; set; } = string.Empty;
    public string? Expediente { get; set; }
    public DateTime? InicioEjecucion { get; set; }
    public DateTime? FinEjecucion { get; set; }
    /// <summary>Monto certificado (columna CERTIFICADO).</summary>
    public decimal? Certificado { get; set; }
    public DateTime? MesAprobacion { get; set; }
    public decimal? PorcEjecFisica { get; set; }
}
