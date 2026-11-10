#nullable enable
namespace IVC.Data.Entities;

/// <summary>Tipo de pago de un certificado (certificado, anticipo, PRD, etc.). Tabla Tipo_Pago.</summary>
public class TipoPago
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
}
