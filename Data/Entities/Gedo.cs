#nullable enable
namespace IVC.Data.Entities;

/// <summary>Documento GEDO de un expediente (número especial ACDIR). Tabla GEDO, bajada externa, sólo lectura y sin clave.</summary>
public class Gedo
{
    /// <summary>Columna NRO_EXPEDIENTE.</summary>
    public string? Expediente { get; set; }
    /// <summary>Columna NRO_ESPECIAL.</summary>
    public string? Acdir { get; set; }
}
