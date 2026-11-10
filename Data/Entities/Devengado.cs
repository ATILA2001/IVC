#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Devengado de SIGAF. Tabla DEVENGADOS, bajada externa, sólo lectura y sin clave. La app
/// suma IMPORTE_PP por expediente (EE_FINANCIERA). SAF mapea la misma tabla con más columnas.
/// </summary>
public class Devengado
{
    /// <summary>Expediente (columna EE_FINANCIERA).</summary>
    public string? EeFinanciera { get; set; }
    /// <summary>Columna IMPORTE_PP.</summary>
    public decimal? ImportePp { get; set; }
}
