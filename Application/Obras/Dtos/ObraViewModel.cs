#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Obras.Dtos;

/// <summary>
/// Fila de la página Obras (ex ObraDTO del legado): los datos de la obra, lo que viene
/// de su proyecto y los indicadores financieros y de ejecución física calculados sobre
/// autorizantes, certificados y legítimos. Sólo los datos de la obra se editan; el
/// resto es derivado y queda fuera de la auditoría.
/// </summary>
public class ObraViewModel
{
    [ExportIgnore, AuditIgnore]
    public int Id { get; set; }

    [ExportIgnore, AuditIgnore]
    public int AreaId { get; set; }

    [Display(Name = "Área")]
    public string? Area { get; set; }

    [ExportIgnore, AuditIgnore]
    public int EmpresaId { get; set; }

    [Display(Name = "Empresa")]
    public string? Empresa { get; set; }

    [ExportIgnore, AuditIgnore]
    public int? ContrataId { get; set; }

    [Display(Name = "Contrata")]
    public string? Contrata { get; set; }

    [Display(Name = "Número")]
    public int? Numero { get; set; }

    [Display(Name = "Año")]
    public int? Anio { get; set; }

    [ExportIgnore, AuditIgnore]
    public int? BarrioId { get; set; }

    [Display(Name = "Barrio")]
    public string? Barrio { get; set; }

    [Display(Name = "Nombre de obra")]
    public string Descripcion { get; set; } = string.Empty;

    [Display(Name = "Etapa")]
    public int? Etapa { get; set; }

    [Display(Name = "Obra N°")]
    public int? ObraNumero { get; set; }

    [Display(Name = "Línea de gestión"), AuditIgnore]
    public string? LineaGestionNombre { get; set; }

    [Display(Name = "Proyecto"), AuditIgnore]
    public string? ProyectoNombre { get; set; }

    // Indicadores (derivados, de sólo lectura).

    /// <summary>Autorizado 2026 del proyecto de la obra ("Disponible actual" en la grilla del legado).</summary>
    [Display(Name = "Disponible actual"), AuditIgnore]
    public decimal? Autorizado2026 { get; set; }

    [Display(Name = "Monto certificado"), AuditIgnore]
    public decimal? MontoCertificado { get; set; }

    [Display(Name = "Porcentaje"), AuditIgnore]
    public decimal? Porcentaje { get; set; }

    [Display(Name = "Monto de obra inicial"), AuditIgnore]
    public decimal? MontoInicial { get; set; }

    [Display(Name = "Monto de obra actual"), AuditIgnore]
    public decimal? MontoActual { get; set; }

    [Display(Name = "Faltante de obra"), AuditIgnore]
    public decimal? MontoFaltante { get; set; }

    [Display(Name = "Fecha inicio"), AuditIgnore]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha fin"), AuditIgnore]
    public DateTime? FechaFin { get; set; }

    [Display(Name = "Ejec. física (%)"), AuditIgnore]
    public decimal? EjecFisica { get; set; }

    [Display(Name = "Faltante fís. (%)"), AuditIgnore]
    public decimal? FaltanteEjecFisica { get; set; }

    /// <summary>Como la grilla del legado: "contrata número/año".</summary>
    [ExportIgnore]
    public string ContrataTexto => $"{Contrata} {Numero}/{Anio}".Trim();
}

/// <summary>
/// Fila de export para quien no ve las columnas financieras (área Redeterminaciones):
/// la planilla sale con las mismas columnas que la grilla le muestra.
/// </summary>
public sealed class ObraExportSinFinanzas
{
    [Display(Name = "Área")] public string? Area { get; set; }
    [Display(Name = "Empresa")] public string? Empresa { get; set; }
    [Display(Name = "Contrata")] public string? Contrata { get; set; }
    [Display(Name = "Número")] public int? Numero { get; set; }
    [Display(Name = "Año")] public int? Anio { get; set; }
    [Display(Name = "Barrio")] public string? Barrio { get; set; }
    [Display(Name = "Nombre de obra")] public string? Descripcion { get; set; }
    [Display(Name = "Etapa")] public int? Etapa { get; set; }
    [Display(Name = "Obra N°")] public int? ObraNumero { get; set; }
    [Display(Name = "Línea de gestión")] public string? LineaGestionNombre { get; set; }
    [Display(Name = "Proyecto")] public string? ProyectoNombre { get; set; }
    [Display(Name = "Fecha inicio")] public DateTime? FechaInicio { get; set; }
    [Display(Name = "Fecha fin")] public DateTime? FechaFin { get; set; }
    [Display(Name = "Ejec. física (%)")] public decimal? EjecFisica { get; set; }
    [Display(Name = "Faltante fís. (%)")] public decimal? FaltanteEjecFisica { get; set; }

    public static ObraExportSinFinanzas De(ObraViewModel o) => new()
    {
        Area = o.Area, Empresa = o.Empresa, Contrata = o.Contrata, Numero = o.Numero, Anio = o.Anio,
        Barrio = o.Barrio, Descripcion = o.Descripcion, Etapa = o.Etapa, ObraNumero = o.ObraNumero,
        LineaGestionNombre = o.LineaGestionNombre, ProyectoNombre = o.ProyectoNombre,
        FechaInicio = o.FechaInicio, FechaFin = o.FechaFin, EjecFisica = o.EjecFisica, FaltanteEjecFisica = o.FaltanteEjecFisica,
    };
}
