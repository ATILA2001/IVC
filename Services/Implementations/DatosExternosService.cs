#nullable enable
using IVC.Application.Redeterminaciones;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;

namespace IVC.Services.Implementations;

public class DatosExternosService(IDatosExternosRepository repo) : IDatosExternosService
{
    public Task<IReadOnlyDictionary<string, UltimoPaseSade>> GetUltimosPasesSadeAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default)
        => repo.GetUltimosPasesSadeAsync(expedientes, ct);

    public Task<IReadOnlyDictionary<string, decimal>> GetDevengadosAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default)
        => repo.GetDevengadosAsync(expedientes, ct);

    public Task<IReadOnlyDictionary<string, string>> GetAcdirsAsync(IReadOnlyCollection<string> expedientes, CancellationToken ct = default)
        => repo.GetAcdirsAsync(expedientes, ct);
}
