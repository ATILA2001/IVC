#nullable enable
namespace IVC.Data.Entities;

/// <summary>Certificado (pago) de un autorizante. Tabla Certificados. Se vincula al autorizante por código.</summary>
public class Certificado
{
    public int Id { get; set; }
    public string CodigoAutorizante { get; set; } = string.Empty;
    public string? ExpedientePago { get; set; }
    public decimal? MontoTotal { get; set; }
    /// <summary>Mes de ejecución de la obra que certifica (lo confirmó el área el 28/10/2026).</summary>
    public DateTime? MesAprobacion { get; set; }
    public int? TipoPagoId { get; set; }
    public TipoPago? TipoPago { get; set; }
    public decimal? PorcEjecFisica { get; set; }
}
