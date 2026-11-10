#nullable enable
namespace IVC.Data.Entities;

/// <summary>Redeterminación de precios de un autorizante. Tabla Redeterminaciones.</summary>
public class Redeterminacion
{
    public int Id { get; set; }
    public string? CodigoAutorizante { get; set; }
    public string? Expediente { get; set; }
    /// <summary>Mes del salto (desde cuándo rige el nuevo precio).</summary>
    public DateTime? Salto { get; set; }
    public int? Nro { get; set; }
    /// <summary>PROVISORIA, DEFINITIVA, etc. Valores inconsistentes en producción (ver análisis del 30/09/2026).</summary>
    public string? Tipo { get; set; }
    /// <summary>Etapa del trámite (columna ETAPA, FK a Estados_Redet).</summary>
    public int? EtapaId { get; set; }
    public EstadoRedet? Etapa { get; set; }
    public string? Observaciones { get; set; }
    /// <summary>Columna calculada en la base (Codigo_Redet = código autorizante + "-R" + Nro): sólo lectura.</summary>
    public string? CodigoRedet { get; set; }
    /// <summary>Porcentaje de ponderación (PORCENTAJE_PONDERACION, decimal(5,2)); hay negativos en producción.</summary>
    public decimal? Porcentaje { get; set; }
    /// <summary>Columna MONTO decimal(5,2), sin uso conocido en el legado; se mapea para no perderla.</summary>
    public decimal? Monto { get; set; }
    /// <summary>Usuario del legado que la cargó (ID_USUARIO, FK a Usuarios).</summary>
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}
