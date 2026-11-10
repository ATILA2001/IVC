#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Catalogos.Dtos;

/// <summary>
/// Fila de la página Movimientos de gestión (tabla MOVIMIENTOS_GESTION): un ajuste de
/// monto sobre el autorizado de una obra. Proyecto, subproyecto y línea vienen del
/// proyecto de la obra; "Autorizado nuevo" es el autorizado 2025 del proyecto más la
/// suma de todos los movimientos de la obra, como lo calculaba el legado.
/// </summary>
public class MovimientoViewModel
{
    [ExportIgnore, AuditIgnore]
    public int Id { get; set; }

    /// <summary>Se elige sólo en el alta; el movimiento no cambia de obra.</summary>
    [ExportIgnore, AuditIgnore]
    public int ObraId { get; set; }

    [Display(Name = "Obra")]
    public string? Obra { get; set; }

    [Display(Name = "Proyecto"), AuditIgnore]
    public string? Proyecto { get; set; }

    [Display(Name = "SubProyecto"), AuditIgnore]
    public string? SubProyecto { get; set; }

    [Display(Name = "Línea de gestión"), AuditIgnore]
    public string? Linea { get; set; }

    [Display(Name = "Movimiento")]
    public decimal? Monto { get; set; }

    [Display(Name = "Fecha")]
    public DateTime? Fecha { get; set; }

    [Display(Name = "Autorizado nuevo"), AuditIgnore]
    public decimal? Autorizado2026 { get; set; }
}
