#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

public interface ILineaGestionRepository
{
    Task<IReadOnlyList<LineaGestion>> GetAllAsync(CancellationToken ct = default);
    Task<LineaGestion?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(LineaGestion entity, CancellationToken ct = default);
    Task UpdateAsync(LineaGestion entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteNombreAsync(string nombre, int idExcluido, CancellationToken ct = default);
}

public interface ILineaGestionFFRepository
{
    /// <summary>Todas las fuentes con su línea cargada (la grilla muestra tipo, grupo y repartición de la línea).</summary>
    Task<IReadOnlyList<LineaGestionFF>> GetAllAsync(CancellationToken ct = default);
    Task<LineaGestionFF?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(LineaGestionFF entity, CancellationToken ct = default);
    Task UpdateAsync(LineaGestionFF entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
