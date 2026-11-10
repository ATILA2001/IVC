#nullable enable
namespace IVC.Data.Entities;

/// <summary>Concepto del autorizante (obra básica, adicional, etc.). Tabla Conceptos.</summary>
public class Concepto
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
}
