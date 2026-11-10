#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

/// <summary>
/// CRUD de los catálogos de ID + NOMBRE (BARRIOS, EMPRESAS). Genérico sobre la entidad:
/// las dos tablas tienen la misma forma y las páginas sólo difieren en el título.
/// </summary>
public interface ICatalogoSimpleRepository
{
    Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken ct = default) where T : class, ICatalogoSimple;
    Task<T?> GetByIdAsync<T>(int id, CancellationToken ct = default) where T : class, ICatalogoSimple;
    Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class, ICatalogoSimple;
    Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class, ICatalogoSimple;
    /// <summary>Borra la fila. Si otra tabla la referencia, SQL Server rechaza el DELETE (FK sin cascada).</summary>
    Task DeleteAsync<T>(int id, CancellationToken ct = default) where T : class, ICatalogoSimple;
    /// <summary>True si ya existe otra fila (distinta de idExcluido) con ese nombre.</summary>
    Task<bool> ExisteNombreAsync<T>(string nombre, int idExcluido, CancellationToken ct = default) where T : class, ICatalogoSimple;
}
