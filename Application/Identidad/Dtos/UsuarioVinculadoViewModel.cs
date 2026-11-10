#nullable enable
using System.ComponentModel.DataAnnotations;
using IVC.Application.Common;

namespace IVC.Application.Identidad.Dtos;

/// <summary>Fila de la grilla de usuarios del panel de configuración.</summary>
public sealed class UsuarioVinculadoViewModel
{
    [AuditIgnore, ExportIgnore]
    public string AuthUserId { get; set; } = string.Empty;

    [Display(Name = "Usuario")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "Planificación habilitada")]
    public bool PlanificacionAbierta { get; set; }
}
