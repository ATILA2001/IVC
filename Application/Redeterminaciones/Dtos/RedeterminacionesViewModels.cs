#nullable enable
using IVC.Application.Common;
using System.ComponentModel.DataAnnotations;

namespace IVC.Application.Redeterminaciones.Dtos;

/// <summary>
/// Fila de la página Autorizantes: un autorizante real o una redeterminación virtual (ex
/// AutorizanteDTO). Las virtuales llevan <see cref="RedeterminacionId"/>, muestran el código
/// de redeterminación, el estado general de su etapa y el monto redeterminado, y no se editan
/// desde esta página.
/// </summary>
public class AutorizanteViewModel
{
    [ExportIgnore, AuditIgnore] public int Id { get; set; }
    [ExportIgnore, AuditIgnore] public int? RedeterminacionId { get; set; }
    [ExportIgnore] public bool EsRedeterminacion => RedeterminacionId is not null;

    /// <summary>Se elige sólo en el alta.</summary>
    [ExportIgnore, AuditIgnore] public int ObraId { get; set; }

    [Display(Name = "Área"), AuditIgnore] public string? Area { get; set; }
    [Display(Name = "Obra"), AuditIgnore] public string? Obra { get; set; }
    [Display(Name = "Contrata"), AuditIgnore] public string? Contrata { get; set; }
    [Display(Name = "Empresa"), AuditIgnore] public string? Empresa { get; set; }
    [Display(Name = "Barrio"), AuditIgnore] public string? Barrio { get; set; }

    /// <summary>Lo asigna el trigger de la base en el alta (OBRA-n).</summary>
    [Display(Name = "Código autorizante")] public string? CodigoAutorizante { get; set; }
    [Display(Name = "Expediente")] public string? Expediente { get; set; }
    [Display(Name = "Detalle")] public string? Detalle { get; set; }
    [ExportIgnore, AuditIgnore] public int? ConceptoId { get; set; }
    [Display(Name = "Concepto")] public string? Concepto { get; set; }
    [Display(Name = "Monto autorizado")] public decimal? MontoAutorizado { get; set; }
    [Display(Name = "Mes aprobación")] public DateTime? MesAprobacion { get; set; }
    [Display(Name = "Mes base")] public DateTime? MesBase { get; set; }
    [ExportIgnore, AuditIgnore] public int? EstadoId { get; set; }
    [Display(Name = "Estado")] public string? Estado { get; set; }

    [Display(Name = "Buzón SADE"), AuditIgnore] public string? BuzonSade { get; set; }
    [Display(Name = "Fecha SADE"), AuditIgnore] public DateTime? FechaSade { get; set; }
    [Display(Name = "GEDO"), AuditIgnore] public string? Acdir { get; set; }
    [Display(Name = "Ejec. física (%)"), AuditIgnore] public decimal? EjecFisica { get; set; }
    [Display(Name = "Faltante fís. (%)"), AuditIgnore] public decimal? FaltanteEjecFisica { get; set; }
}

/// <summary>
/// Fila de la página Certificados: un certificado real o una reliquidación virtual (ex
/// CertificadoDTO). Las reliquidaciones tienen Id 0, tipo de pago RELIQUIDACION y se
/// identifican por código de redeterminación y mes; sólo su expediente se edita, y se
/// guarda en EXPEDIENTES_RELIQ.
/// </summary>
public class CertificadoViewModel
{
    [ExportIgnore, AuditIgnore] public int Id { get; set; }
    /// <summary>Se elige sólo en el alta.</summary>
    [ExportIgnore, AuditIgnore] public int? AutorizanteId { get; set; }
    [ExportIgnore] public bool EsReliquidacion => Id == 0 && TipoPagoId == ReglasRedeterminaciones.TipoPagoReliquidacionId;
    /// <summary>Clave estable de una reliquidación: código de redeterminación y mes.</summary>
    [ExportIgnore] public string? ClaveReliquidacion =>
        EsReliquidacion && MesAprobacion is DateTime mes ? $"{CodigoAutorizante}|{mes:yyyyMM}" : null;

    [Display(Name = "Área"), AuditIgnore] public string? Area { get; set; }
    [Display(Name = "Contrata"), AuditIgnore] public string? Contrata { get; set; }
    [Display(Name = "Obra"), AuditIgnore] public string? Obra { get; set; }
    [Display(Name = "Barrio"), AuditIgnore] public string? Barrio { get; set; }
    [Display(Name = "Proyecto"), AuditIgnore] public string? Proyecto { get; set; }
    [Display(Name = "Empresa"), AuditIgnore] public string? Empresa { get; set; }
    [Display(Name = "Código autorizante")] public string? CodigoAutorizante { get; set; }
    [Display(Name = "Estado autorizante"), AuditIgnore] public string? AutorizanteEstado { get; set; }
    [Display(Name = "Expediente")] public string? ExpedientePago { get; set; }
    [Display(Name = "Estado pago"), AuditIgnore] public string? Estado { get; set; }
    [ExportIgnore, AuditIgnore] public int? TipoPagoId { get; set; }
    [Display(Name = "Tipo")] public string? TipoPago { get; set; }
    [Display(Name = "Monto certificado")] public decimal? MontoTotal { get; set; }
    [Display(Name = "Mes certificado")] public DateTime? MesAprobacion { get; set; }
    [Display(Name = "Línea de gestión"), AuditIgnore] public string? LineaGestion { get; set; }
    [Display(Name = "Porcentaje"), AuditIgnore] public decimal? Porcentaje { get; set; }
    [Display(Name = "Ejec. física (%)")] public decimal? PorcEjecFisica { get; set; }
    [Display(Name = "SIGAF"), AuditIgnore] public decimal? Sigaf { get; set; }
    [Display(Name = "Buzón SADE"), AuditIgnore] public string? BuzonSade { get; set; }
    [Display(Name = "Fecha SADE"), AuditIgnore] public DateTime? FechaSade { get; set; }
}

/// <summary>Fila de la página Legítimos abonos (tabla LEGITIMOS_ABONOS) con su obra, SIGAF y SADE.</summary>
public class LegitimoViewModel
{
    [ExportIgnore, AuditIgnore] public int Id { get; set; }
    /// <summary>Se elige en el alta; no se cambia al editar (como el legado).</summary>
    [ExportIgnore, AuditIgnore] public int ObraId { get; set; }

    [Display(Name = "Área"), AuditIgnore] public string? Area { get; set; }
    [Display(Name = "Obra"), AuditIgnore] public string? Obra { get; set; }
    [Display(Name = "Barrio"), AuditIgnore] public string? Barrio { get; set; }
    [Display(Name = "Empresa"), AuditIgnore] public string? Empresa { get; set; }
    [Display(Name = "Línea de gestión"), AuditIgnore] public string? LineaGestion { get; set; }
    [Display(Name = "Código autorizante")] public string? CodigoAutorizante { get; set; }
    [Display(Name = "Expediente")] public string? Expediente { get; set; }
    [Display(Name = "Inicio ejecución")] public DateTime? InicioEjecucion { get; set; }
    [Display(Name = "Fin ejecución")] public DateTime? FinEjecucion { get; set; }
    [Display(Name = "Monto certificado")] public decimal? Certificado { get; set; }
    [Display(Name = "Mes aprobación")] public DateTime? MesAprobacion { get; set; }
    [Display(Name = "Estado"), AuditIgnore] public string? Estado { get; set; }
    [Display(Name = "SIGAF"), AuditIgnore] public decimal? Sigaf { get; set; }
    [Display(Name = "Buzón SADE"), AuditIgnore] public string? BuzonSade { get; set; }
    [Display(Name = "Fecha SADE"), AuditIgnore] public DateTime? FechaSade { get; set; }
}

/// <summary>Fila de la página Redeterminaciones (tabla REDETERMINACIONES) con su autorizante, etapa, usuario y seguimiento SADE.</summary>
public class RedeterminacionViewModel
{
    [ExportIgnore, AuditIgnore] public int Id { get; set; }
    /// <summary>Se elige sólo en el alta.</summary>
    [ExportIgnore, AuditIgnore] public int? AutorizanteId { get; set; }

    [Display(Name = "Código redeterminación"), AuditIgnore] public string? CodigoRedet { get; set; }
    [Display(Name = "Código autorizante")] public string? CodigoAutorizante { get; set; }
    [Display(Name = "Obra"), AuditIgnore] public string? Obra { get; set; }
    [Display(Name = "Empresa"), AuditIgnore] public string? Empresa { get; set; }
    [Display(Name = "Contrata"), AuditIgnore] public string? Contrata { get; set; }
    [Display(Name = "Área"), AuditIgnore] public string? Area { get; set; }
    [ExportIgnore, AuditIgnore] public int? EtapaId { get; set; }
    [Display(Name = "Etapa")] public string? Etapa { get; set; }
    [Display(Name = "Expediente")] public string? Expediente { get; set; }
    [Display(Name = "Tipo")] public string? Tipo { get; set; }
    [Display(Name = "Salto")] public DateTime? Salto { get; set; }
    [Display(Name = "Nro")] public int? Nro { get; set; }
    [Display(Name = "Porcentaje")] public decimal? Porcentaje { get; set; }
    [Display(Name = "Observaciones")] public string? Observaciones { get; set; }
    [ExportIgnore, AuditIgnore] public int? UsuarioId { get; set; }
    [Display(Name = "Usuario")] public string? Usuario { get; set; }
    [Display(Name = "Buzón SADE"), AuditIgnore] public string? BuzonSade { get; set; }
    [Display(Name = "Fecha SADE"), AuditIgnore] public DateTime? FechaSade { get; set; }
    [Display(Name = "Días en buzón"), AuditIgnore] public int? DiasEnBuzon { get; set; }
    /// <summary>El buzón del último pase no corresponde a la etapa ("control de estados").</summary>
    [ExportIgnore, AuditIgnore] public bool Discrepancia { get; set; }
}
