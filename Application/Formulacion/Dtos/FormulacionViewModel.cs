#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Formulacion.Dtos;

/// <summary>
/// Fila de la página Formulación (ex FormulacionPivotEF): una fila lógica por obra que
/// agrupa sus (hasta) tres filas FORMULACION del ciclo vigente. Los campos compartidos son
/// idénticos en las filas reales de la obra (se escriben iguales en las tres); los montos
/// son posicionales: año 1 = año base, año 2 = base+1, año 3 = base+2. La clave es la obra,
/// no hay Id propio.
/// </summary>
public class FormulacionViewModel
{
    /// <summary>Obra (clave de la fila lógica).</summary>
    [ExportIgnore, AuditIgnore]
    public int ObraId { get; set; }

    [Display(Name = "Área"), AuditIgnore]
    public string? Area { get; set; }

    [Display(Name = "Empresa"), AuditIgnore]
    public string? Empresa { get; set; }

    /// <summary>Como la grilla del legado: "contrata número/año".</summary>
    [Display(Name = "Contrata"), AuditIgnore]
    public string? Contrata { get; set; }

    [Display(Name = "Barrio"), AuditIgnore]
    public string? Barrio { get; set; }

    [Display(Name = "Línea de gestión"), AuditIgnore]
    public string? LineaGestion { get; set; }

    [Display(Name = "Proyecto"), AuditIgnore]
    public string? Proyecto { get; set; }

    [Display(Name = "Nombre de obra")]
    public string? Obra { get; set; }

    [Display(Name = "Breve descripción")]
    public string? BreveDescripcion { get; set; }

    [Display(Name = "Fecha inicio")]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha fin")]
    public DateTime? FechaFin { get; set; }

    /// <summary>Sólo el administrador lo ve y lo edita (como en el legado).</summary>
    [Display(Name = "PPI")]
    public int? Ppi { get; set; }

    [Display(Name = "Techo")]
    public decimal? Techos { get; set; }

    // Los títulos de las tres columnas de monto llevan el año del ciclo: la página los
    // resuelve en pantalla y en el export (ver FormulacionCiclo).
    [Display(Name = "Monto año 1")]
    public decimal? MontoAnio1 { get; set; }

    [Display(Name = "Monto año 2")]
    public decimal? MontoAnio2 { get; set; }

    [Display(Name = "Monto año 3")]
    public decimal? MontoAnio3 { get; set; }

    [Display(Name = "Mes base")]
    public DateTime? MesBase { get; set; }

    [ExportIgnore, AuditIgnore]
    public int? UnidadMedidaId { get; set; }

    [Display(Name = "Unidad de medida")]
    public string? UnidadMedida { get; set; }

    [Display(Name = "Valor de medida")]
    public decimal? ValorMedida { get; set; }

    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    [ExportIgnore, AuditIgnore]
    public int? PrioridadId { get; set; }

    [Display(Name = "Prioridad")]
    public string? Prioridad { get; set; }

    /// <summary>Suma de los montos de los 3 años (los nulos cuentan como 0).</summary>
    [ExportIgnore, AuditIgnore]
    public decimal TotalMonto => (MontoAnio1 ?? 0m) + (MontoAnio2 ?? 0m) + (MontoAnio3 ?? 0m);

    /// <summary>Montos por posición del ciclo, en el orden que persiste el servicio.</summary>
    [ExportIgnore, AuditIgnore]
    public decimal?[] Montos => [MontoAnio1, MontoAnio2, MontoAnio3];
}

/// <summary>
/// Fila de export para quien no ve el PPI (usuario no administrador): la planilla sale con
/// las mismas columnas que la grilla le muestra. Las propiedades de monto conservan el
/// nombre para que la página les ponga el año del ciclo como título.
/// </summary>
public sealed class FormulacionExportSinPpi
{
    [Display(Name = "Área")] public string? Area { get; set; }
    [Display(Name = "Empresa")] public string? Empresa { get; set; }
    [Display(Name = "Contrata")] public string? Contrata { get; set; }
    [Display(Name = "Barrio")] public string? Barrio { get; set; }
    [Display(Name = "Línea de gestión")] public string? LineaGestion { get; set; }
    [Display(Name = "Proyecto")] public string? Proyecto { get; set; }
    [Display(Name = "Nombre de obra")] public string? Obra { get; set; }
    [Display(Name = "Breve descripción")] public string? BreveDescripcion { get; set; }
    [Display(Name = "Fecha inicio")] public DateTime? FechaInicio { get; set; }
    [Display(Name = "Fecha fin")] public DateTime? FechaFin { get; set; }
    [Display(Name = "Techo")] public decimal? Techos { get; set; }
    [Display(Name = "Monto año 1")] public decimal? MontoAnio1 { get; set; }
    [Display(Name = "Monto año 2")] public decimal? MontoAnio2 { get; set; }
    [Display(Name = "Monto año 3")] public decimal? MontoAnio3 { get; set; }
    [Display(Name = "Mes base")] public DateTime? MesBase { get; set; }
    [Display(Name = "Unidad de medida")] public string? UnidadMedida { get; set; }
    [Display(Name = "Valor de medida")] public decimal? ValorMedida { get; set; }
    [Display(Name = "Observaciones")] public string? Observaciones { get; set; }
    [Display(Name = "Prioridad")] public string? Prioridad { get; set; }

    public static FormulacionExportSinPpi De(FormulacionViewModel f) => new()
    {
        Area = f.Area, Empresa = f.Empresa, Contrata = f.Contrata, Barrio = f.Barrio, LineaGestion = f.LineaGestion,
        Proyecto = f.Proyecto, Obra = f.Obra, BreveDescripcion = f.BreveDescripcion, FechaInicio = f.FechaInicio, FechaFin = f.FechaFin,
        Techos = f.Techos, MontoAnio1 = f.MontoAnio1, MontoAnio2 = f.MontoAnio2, MontoAnio3 = f.MontoAnio3, MesBase = f.MesBase,
        UnidadMedida = f.UnidadMedida, ValorMedida = f.ValorMedida, Observaciones = f.Observaciones, Prioridad = f.Prioridad,
    };
}
