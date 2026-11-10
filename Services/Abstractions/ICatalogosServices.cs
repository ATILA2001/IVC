#nullable enable
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Data.Entities;

namespace IVC.Services.Abstractions;

/// <summary>
/// Alta, edición y baja de un catálogo de sólo nombre. Una implementación genérica
/// sirve a Barrios y Empresas: la página elige la entidad con el parámetro de tipo.
/// </summary>
public interface ICatalogoSimpleService<TEntity> where TEntity : class, ICatalogoSimple, new()
{
    Task<IReadOnlyList<CatalogoSimpleViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<CatalogoSimpleViewModel> CreateAsync(CatalogoSimpleViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(CatalogoSimpleViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ILineasGestionService
{
    Task<IReadOnlyList<LineaGestionViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<LineaGestionViewModel> CreateAsync(LineaGestionViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(LineaGestionViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ILineasGestionFFService
{
    Task<IReadOnlyList<LineaGestionFFViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<LineaGestionFFViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<LineaGestionFFViewModel> CreateAsync(LineaGestionFFViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(LineaGestionFFViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IProyectosService
{
    Task<IReadOnlyList<ProyectoViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<ProyectoViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProyectoViewModel> CreateAsync(ProyectoViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(ProyectoViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IMovimientosService
{
    Task<IReadOnlyList<MovimientoViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<MovimientoViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MovimientoViewModel> CreateAsync(MovimientoViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(MovimientoViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>Opciones de los desplegables de las páginas.</summary>
public interface ILookupService
{
    Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetObrasSinProyectoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetLineasGestionAsync(CancellationToken ct = default);
}
