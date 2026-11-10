#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Catalogos.Dtos;

/// <summary>Fila de la página Líneas de gestión (tabla LINEA_DE_GESTION).</summary>
public class LineaGestionViewModel
{
    [ExportIgnore, AuditIgnore]
    public int Id { get; set; }

    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "Tipo")]
    public string? Tipo { get; set; }

    [Display(Name = "Grupo")]
    public string? Grupo { get; set; }

    [Display(Name = "Repartición")]
    public string? Reparticion { get; set; }
}
