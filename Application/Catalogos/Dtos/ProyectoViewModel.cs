#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Catalogos.Dtos;

/// <summary>
/// Fila de la página Proyectos (tabla BD_PROYECTOS): una fila por obra, con los datos de
/// la obra como contexto de sólo lectura (área, contrata, empresa) y lo propio del
/// proyecto editable. Autorizado 2025 es histórico y no se edita desde la página.
/// </summary>
public class ProyectoViewModel
{
    [ExportIgnore, AuditIgnore]
    public int Id { get; set; }

    /// <summary>Se elige sólo en el alta: una obra tiene a lo sumo un proyecto.</summary>
    [ExportIgnore, AuditIgnore]
    public int ObraId { get; set; }

    [Display(Name = "Área"), AuditIgnore]
    public string? Area { get; set; }

    [Display(Name = "Contrata"), AuditIgnore]
    public string? Contrata { get; set; }

    [Display(Name = "Obra")]
    public string? Obra { get; set; }

    [Display(Name = "Empresa"), AuditIgnore]
    public string? Empresa { get; set; }

    [Display(Name = "Proyecto")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "SubProyecto")]
    public string? SubProyecto { get; set; }

    [ExportIgnore, AuditIgnore]
    public int? LineaGestionId { get; set; }

    [Display(Name = "Línea de gestión")]
    public string? LineaGestionNombre { get; set; }

    [Display(Name = "Autorizado 2025")]
    public decimal? Autorizado2025 { get; set; }

    [Display(Name = "Autorizado 2026")]
    public decimal? Autorizado2026 { get; set; }
}
