#nullable enable
using IVC.Application.Obras;
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

/// <summary>OBRAS con sus catálogos y su proyecto, y los agregados para los indicadores de la grilla.</summary>
public interface IObraRepository
{
    /// <summary>Obras de las áreas indicadas, o todas si <paramref name="areaIds"/> es null. Ordenadas por descripción.</summary>
    Task<IReadOnlyList<Obra>> GetAllAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default);
    Task<Obra?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Obra entity, CancellationToken ct = default);
    Task UpdateAsync(Obra entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Agregados por obra (autorizantes, legítimos, certificados vinculados por código de
    /// autorizante y ejecución física) para las obras indicadas, o para todas si es null.
    /// Las obras sin movimientos no aparecen en el diccionario.
    /// </summary>
    Task<IReadOnlyDictionary<int, AgregadosObra>> GetAgregadosAsync(IReadOnlyCollection<int>? obraIds, CancellationToken ct = default);
}
