#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Interruptores globales de planificación y formulación. Tabla ABM_PLANI, una sola fila
/// (el legado toma la primera y la crea si no existe).
/// </summary>
public class AbmPlani
{
    public int Id { get; set; }
    /// <summary>Columna PLANIFICACION_ABIERTA.</summary>
    public bool PlanificacionAbierta { get; set; }
    /// <summary>Columna FORMULACION_ABIERTA.</summary>
    public bool FormulacionAbierta { get; set; }
}
