#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Formulación presupuestaria de una obra para un período. Tabla FORMULACION. Una obra
/// tiene hasta tres filas por ciclo (año base, base+1, base+2) que la UI muestra pivoteadas.
/// </summary>
public class Formulacion
{
    public const int ObservacionesMaxLength = 255;
    public const int BreveDescripcionMaxLength = 500;

    public int Id { get; set; }
    /// <summary>Obra (columna ID_BASE).</summary>
    public int ObraId { get; set; }
    public Obra Obra { get; set; } = null!;
    public DateTime FechaPeriodo { get; set; }
    public decimal? Monto { get; set; }
    public DateTime? MesBase { get; set; }
    public string? Observaciones { get; set; }
    public decimal? ValorMedida { get; set; }
    public int? Ppi { get; set; }
    public decimal? Techos { get; set; }
    public int? UnidadMedidaId { get; set; }
    public UnidadMedida? UnidadMedida { get; set; }
    public int? PrioridadId { get; set; }
    public Prioridad? Prioridad { get; set; }
    public string? BreveDescripcion { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
