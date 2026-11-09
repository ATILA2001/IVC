#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Movimiento presupuestario de una obra. Tabla MOVIMIENTOS_GESTION (ID, ID_BASE, MOVIMIENTO, FECHA).
/// El legado tenía una entidad EF apuntando a una tabla "Movimientos" que no es la que usa la página.
/// </summary>
public class MovimientoGestion
{
    public int Id { get; set; }
    /// <summary>Obra (columna ID_BASE).</summary>
    public int ObraId { get; set; }
    public Obra Obra { get; set; } = null!;
    /// <summary>Importe del movimiento (columna MOVIMIENTO).</summary>
    public decimal? Monto { get; set; }
    public DateTime? Fecha { get; set; }
}
