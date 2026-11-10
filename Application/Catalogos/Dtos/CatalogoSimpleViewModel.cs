#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Catalogos.Dtos;

/// <summary>
/// Fila de los catálogos de sólo nombre (Barrios, Empresas). Un único ViewModel para
/// las dos páginas: comparten columnas, reglas y servicio.
/// </summary>
public class CatalogoSimpleViewModel
{
    [ExportIgnore, AuditIgnore]
    public int Id { get; set; }

    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;
}
