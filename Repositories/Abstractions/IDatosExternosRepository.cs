#nullable enable
using IVC.Application.Redeterminaciones;

namespace IVC.Repositories.Abstractions;

/// <summary>
/// Datos externos que alimentan las grillas de redeterminaciones: último pase SADE
/// (PASES_SADE), devengado SIGAF (DEVENGADOS) y número especial ACDIR (GEDO). Único lugar
/// que consulta esas tablas. Los resultados sólo incluyen los expedientes que tienen datos.
/// </summary>
public interface IDatosExternosRepository
{
    /// <summary>Último pase SADE por expediente (el de mayor FECHA ULTIMO PASE).</summary>
    Task<IReadOnlyDictionary<string, UltimoPaseSade>> GetUltimosPasesSadeAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default);

    /// <summary>Suma de IMPORTE_PP devengado por expediente (EE_FINANCIERA).</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetDevengadosAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default);

    /// <summary>Número especial ACDIR (GEDO) de la firma más reciente por expediente.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAcdirsAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default);
}
