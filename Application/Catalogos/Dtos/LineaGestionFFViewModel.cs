#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Catalogos.Dtos;

/// <summary>
/// Fila de la página Líneas de gestión FF (tabla LINEA_DE_GESTION_FF): una fuente de
/// financiamiento de una línea. Tipo, Grupo y Repartición son de la línea y se muestran
/// como contexto, igual que en la grilla del legado.
/// </summary>
public class LineaGestionFFViewModel
{
    [ExportIgnore, AuditIgnore]
    public int Id { get; set; }

    /// <summary>Lo que edita el desplegable; el historial registra el nombre visible.</summary>
    [ExportIgnore, AuditIgnore]
    public int LineaGestionId { get; set; }

    [Display(Name = "Línea de gestión")]
    public string? LineaGestionNombre { get; set; }

    [Display(Name = "Nombre FF")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "Fuente")]
    public string? Fuente { get; set; }

    [Display(Name = "Tipo"), AuditIgnore]
    public string? Tipo { get; set; }

    [Display(Name = "Grupo"), AuditIgnore]
    public string? Grupo { get; set; }

    [Display(Name = "Repartición"), AuditIgnore]
    public string? Reparticion { get; set; }
}
