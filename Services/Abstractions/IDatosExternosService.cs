#nullable enable
using IVC.Application.Redeterminaciones;

namespace IVC.Services.Abstractions;

/// <summary>SADE, SIGAF y ACDIR por expediente, en bloque. Sólo los expedientes con datos.</summary>
public interface IDatosExternosService
{
    Task<IReadOnlyDictionary<string, UltimoPaseSade>> GetUltimosPasesSadeAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, decimal>> GetDevengadosAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAcdirsAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default);
}
