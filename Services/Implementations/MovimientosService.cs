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
/// MOVIMIENTOS_GESTION: ajustes de monto por obra. "Autorizado nuevo" se calcula como
/// en el legado: autorizado 2025 del proyecto de la obra más la suma de TODOS los
/// movimientos de esa obra (null si la obra no tiene proyecto).
/// </summary>
public class MovimientosService(IMovimientoRepository repo, ILogger<MovimientosService> logger) : IMovimientosService
{
    public async Task<IReadOnlyList<MovimientoViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var filas = await repo.GetAllAsync(ct);
        // La lista trae todos los movimientos: la suma por obra sale de la misma lista.
        var sumaPorObra = filas.GroupBy(m => m.ObraId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Monto ?? 0m));
        return filas.Select(m => ToVm(m, sumaPorObra[m.ObraId])).ToList();
    }

    public async Task<MovimientoViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var fila = await repo.GetByIdAsync(id, ct);
        if (fila is null) return null;
        var suma = await repo.SumarMontoPorObraAsync(fila.ObraId, ct) ?? 0m;
        return ToVm(fila, suma);
    }

    public async Task<MovimientoViewModel> CreateAsync(MovimientoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: true);
        var entity = new MovimientoGestion { ObraId = vm.ObraId, Monto = vm.Monto, Fecha = vm.Fecha!.Value.Date };
        await repo.AddAsync(entity, ct);
        return await GetByIdAsync(entity.Id, ct) ?? ToVm(entity, entity.Monto ?? 0m);
    }

    public async Task UpdateAsync(MovimientoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: false);
        var actual = await repo.GetByIdAsync(vm.Id, ct);
        if (actual is null)
        {
            logger.LogWarning("Movimiento {Id} ya no existe; no se guardaron los cambios.", vm.Id);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó este movimiento mientras lo editabas. Se recargaron los datos.");
        }
        // Entidad sin navegaciones; la obra no cambia.
        var entity = new MovimientoGestion { Id = actual.Id, ObraId = actual.ObraId, Monto = vm.Monto, Fecha = vm.Fecha!.Value.Date };
        await repo.UpdateAsync(entity, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await repo.DeleteAsync(id, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo eliminar el movimiento {Id}.", id);
            throw new ArgumentException("No se puede eliminar: el movimiento está en uso en otros registros.");
        }
    }

    private static void Validar(MovimientoViewModel vm, bool esAlta)
    {
        var errores = MovimientoValidator.Validar(vm, esAlta);
        if (errores.Count > 0) throw new ArgumentException(string.Join(" ", errores));
    }

    internal static MovimientoViewModel ToVm(MovimientoGestion m, decimal sumaObra)
    {
        var proyecto = m.Obra?.Proyecto;
        return new MovimientoViewModel
        {
            Id = m.Id,
            ObraId = m.ObraId,
            Obra = ProyectosService.TextoObra(m.Obra),
            Proyecto = proyecto?.Nombre,
            SubProyecto = proyecto?.SubProyecto,
            Linea = proyecto?.LineaGestion?.Nombre,
            Monto = m.Monto,
            Fecha = m.Fecha,
            // BD.AUTORIZADO2025 + ISNULL(SUM(...), 0): sin proyecto (LEFT JOIN) queda null.
            Autorizado2026 = proyecto?.Autorizado2025 is decimal base2025 ? base2025 + sumaObra : null,
        };
    }
}

public class LookupService(ILookupRepository repo) : ILookupService
{
    public Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(CancellationToken ct = default) => repo.GetObrasAsync(ct);
    public Task<IReadOnlyList<OpcionLookup>> GetObrasSinProyectoAsync(CancellationToken ct = default) => repo.GetObrasSinProyectoAsync(ct);
    public Task<IReadOnlyList<OpcionLookup>> GetLineasGestionAsync(CancellationToken ct = default) => repo.GetLineasGestionAsync(ct);
    public Task<IReadOnlyList<OpcionLookup>> GetAreasAsync(CancellationToken ct = default) => repo.GetAreasAsync(ct);
    public Task<IReadOnlyList<OpcionLookup>> GetEmpresasAsync(CancellationToken ct = default) => repo.GetEmpresasAsync(ct);
    public Task<IReadOnlyList<OpcionLookup>> GetContratasAsync(CancellationToken ct = default) => repo.GetContratasAsync(ct);
    public Task<IReadOnlyList<OpcionLookup>> GetBarriosAsync(CancellationToken ct = default) => repo.GetBarriosAsync(ct);
}
