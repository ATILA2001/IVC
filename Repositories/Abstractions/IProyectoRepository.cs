#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

/// <summary>BD_PROYECTOS: una fila por obra, con la obra y sus catálogos cargados para la grilla.</summary>
public interface IProyectoRepository
{
    Task<IReadOnlyList<Proyecto>> GetAllAsync(CancellationToken ct = default);
    Task<Proyecto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Proyecto entity, CancellationToken ct = default);
    Task UpdateAsync(Proyecto entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>True si la obra ya tiene proyecto (ID_BASE es único en la base).</summary>
    Task<bool> ExisteParaObraAsync(int obraId, CancellationToken ct = default);
}

/// <summary>MOVIMIENTOS_GESTION: ajustes de monto por obra, con la obra y su proyecto cargados.</summary>
public interface IMovimientoRepository
{
    Task<IReadOnlyList<MovimientoGestion>> GetAllAsync(CancellationToken ct = default);
    Task<MovimientoGestion?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(MovimientoGestion entity, CancellationToken ct = default);
    Task UpdateAsync(MovimientoGestion entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Suma de todos los movimientos de la obra (null si no tiene ninguno).</summary>
    Task<decimal?> SumarMontoPorObraAsync(int obraId, CancellationToken ct = default);
}
