#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Expediente de pago de una reliquidación (fila virtual que no existe en Certificados).
/// Tabla EXPEDIENTES_RELIQ: se identifica por código de redeterminación y mes.
/// </summary>
public class ExpedienteReliq
{
    public int Id { get; set; }
    public string CodigoRedet { get; set; } = string.Empty;
    public DateTime MesAprobacion { get; set; }
    public string? Expediente { get; set; }
}
