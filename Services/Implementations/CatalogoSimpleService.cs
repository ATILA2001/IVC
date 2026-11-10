#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Services.Implementations;

/// <summary>
/// CRUD de los catálogos de sólo nombre (BARRIOS, EMPRESAS). El legado borraba sin
/// confirmar y, si la fila estaba en uso, tragaba el error: acá el nombre se valida y
/// se exige único, y un borrado rechazado por la base vuelve como mensaje al usuario.
/// </summary>
public class CatalogoSimpleService<TEntity>(ICatalogoSimpleRepository repo, ILogger<CatalogoSimpleService<TEntity>> logger)
    : ICatalogoSimpleService<TEntity> where TEntity : class, ICatalogoSimple, new()
{
    public async Task<IReadOnlyList<CatalogoSimpleViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var filas = await repo.GetAllAsync<TEntity>(ct);
        return filas.Select(ToVm).ToList();
    }

    public async Task<CatalogoSimpleViewModel> CreateAsync(CatalogoSimpleViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ValidarNombreUnicoAsync(vm, ct);
        var entity = new TEntity { Nombre = vm.Nombre.Trim() };
        await repo.AddAsync(entity, ct);
        return ToVm(entity);
    }

    public async Task UpdateAsync(CatalogoSimpleViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ValidarNombreUnicoAsync(vm, ct);
        var entity = await repo.GetByIdAsync<TEntity>(vm.Id, ct);
        if (entity is null)
        {
            // Otro usuario la borró mientras ésta se editaba: avisar como conflicto;
            // un retorno silencioso dejaría creer que se guardó.
            logger.LogWarning("{Catalogo} {Id} ya no existe; no se guardaron los cambios.", typeof(TEntity).Name, vm.Id);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó esta fila mientras la editabas. Se recargaron los datos.");
        }
        entity.Nombre = vm.Nombre.Trim();
        await repo.UpdateAsync(entity, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await repo.DeleteAsync<TEntity>(id, ct);
        }
        catch (DbUpdateException ex)
        {
            // OBRAS referencia a BARRIOS y EMPRESAS sin cascada: si la fila está en uso,
            // el DELETE falla. El legado devolvía false y no decía nada.
            logger.LogWarning(ex, "No se pudo eliminar {Catalogo} {Id} (en uso).", typeof(TEntity).Name, id);
            throw new ArgumentException("No se puede eliminar: está en uso en obras u otros registros.");
        }
    }

    private static void Validar(CatalogoSimpleViewModel vm)
    {
        var errores = CatalogoSimpleValidator.Validar(vm);
        if (errores.Count > 0) throw new ArgumentException(string.Join(" ", errores));
    }

    private async Task ValidarNombreUnicoAsync(CatalogoSimpleViewModel vm, CancellationToken ct)
    {
        if (await repo.ExisteNombreAsync<TEntity>(vm.Nombre.Trim(), vm.Id, ct))
            throw new ArgumentException($"Ya existe \"{vm.Nombre.Trim()}\" en este catálogo.");
    }

    private static CatalogoSimpleViewModel ToVm(TEntity e) => new() { Id = e.Id, Nombre = e.Nombre ?? string.Empty };
}
