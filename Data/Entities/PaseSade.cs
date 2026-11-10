#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Pase de un expediente en SADE. Tabla PASES_SADE, bajada externa, sólo lectura y sin clave:
/// el legado la mapeaba con [Key] en EXPEDIENTE, que no es único, y EF devolvía la misma
/// instancia para todas las filas de un expediente (hallazgo crítico de la auditoría del 31/10/2026).
/// </summary>
public class PaseSade
{
    /// <summary>Columna EXPEDIENTE (varchar: comparar con parámetros varchar para no romper la collation).</summary>
    public string? Expediente { get; set; }
    /// <summary>Columna "BUZON DESTINO".</summary>
    public string? BuzonDestino { get; set; }
    /// <summary>Columna "FECHA ULTIMO PASE".</summary>
    public DateTime? FechaUltimoPase { get; set; }
}
