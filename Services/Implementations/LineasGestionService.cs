#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Services.Implementations;

public class LineasGestionService(ILineaGestionRepository repo, ILogger<LineasGestionService> logger) : ILineasGestionService
{
    public async Task<IReadOnlyList<LineaGestionViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var filas = await repo.GetAllAsync(ct);
        return filas.Select(ToVm).ToList();
    }

    public async Task<LineaGestionViewModel> CreateAsync(LineaGestionViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ValidarNombreUnicoAsync(vm, ct);
        var entity = Aplicar(vm, new LineaGestion());
        await repo.AddAsync(entity, ct);
        return ToVm(entity);
    }

    public async Task UpdateAsync(LineaGestionViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ValidarNombreUnicoAsync(vm, ct);
        var entity = await repo.GetByIdAsync(vm.Id, ct);
        if (entity is null)
        {
            logger.LogWarning("Línea de gestión {Id} ya no existe; no se guardaron los cambios.", vm.Id);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó esta línea mientras la editabas. Se recargaron los datos.");
        }
        await repo.UpdateAsync(Aplicar(vm, entity), ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await repo.DeleteAsync(id, ct);
        }
        catch (DbUpdateException ex)
        {
            // BD_PROYECTOS y LINEA_DE_GESTION_FF la referencian sin cascada.
            logger.LogWarning(ex, "No se pudo eliminar la línea de gestión {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: la línea está en uso en proyectos o fuentes de financiamiento.");
        }
    }

    private static void Validar(LineaGestionViewModel vm)
    {
        var errores = LineaGestionValidator.Validar(vm);
        if (errores.Count > 0) throw new ArgumentException(string.Join(" ", errores));
    }

    private async Task ValidarNombreUnicoAsync(LineaGestionViewModel vm, CancellationToken ct)
    {
        if (await repo.ExisteNombreAsync(vm.Nombre.Trim(), vm.Id, ct))
            throw new ArgumentException($"Ya existe una línea de gestión \"{vm.Nombre.Trim()}\".");
    }

    private static LineaGestion Aplicar(LineaGestionViewModel vm, LineaGestion e)
    {
        e.Nombre = vm.Nombre.Trim();
        e.Tipo = Limpio(vm.Tipo);
        e.Grupo = Limpio(vm.Grupo);
        e.Reparticion = Limpio(vm.Reparticion);
        return e;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    internal static LineaGestionViewModel ToVm(LineaGestion e) => new()
    {
        Id = e.Id,
        Nombre = e.Nombre ?? string.Empty,
        Tipo = e.Tipo,
        Grupo = e.Grupo,
        Reparticion = e.Reparticion,
    };
}

public class LineasGestionFFService(
    ILineaGestionFFRepository repo,
    ILineaGestionRepository lineas,
    ILogger<LineasGestionFFService> logger) : ILineasGestionFFService
{
    public async Task<IReadOnlyList<LineaGestionFFViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var filas = await repo.GetAllAsync(ct);
        return filas.Select(ToVm).ToList();
    }

    public async Task<LineaGestionFFViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var fila = await repo.GetByIdAsync(id, ct);
        return fila is null ? null : ToVm(fila);
    }

    public async Task<LineaGestionFFViewModel> CreateAsync(LineaGestionFFViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        var linea = await LineaAsync(vm.LineaGestionId, ct);
        var entity = new LineaGestionFF { LineaGestionId = linea.Id, Nombre = vm.Nombre.Trim(), Fuente = Limpio(vm.Fuente) };
        await repo.AddAsync(entity, ct);
        entity.LineaGestion = linea;
        return ToVm(entity);
    }

    public async Task UpdateAsync(LineaGestionFFViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        var linea = await LineaAsync(vm.LineaGestionId, ct);
        var actual = await repo.GetByIdAsync(vm.Id, ct);
        if (actual is null)
        {
            logger.LogWarning("Línea de gestión FF {Id} ya no existe; no se guardaron los cambios.", vm.Id);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó esta fuente mientras la editabas. Se recargaron los datos.");
        }
        // Entidad sin navegaciones: Update marca como modificado todo el grafo que
        // alcanza, y la línea cargada por Include no debe volver a escribirse.
        var entity = new LineaGestionFF { Id = actual.Id, LineaGestionId = linea.Id, Nombre = vm.Nombre.Trim(), Fuente = Limpio(vm.Fuente) };
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
            logger.LogWarning(ex, "No se pudo eliminar la línea de gestión FF {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: la fuente está en uso en otros registros.");
        }
    }

    private static void Validar(LineaGestionFFViewModel vm)
    {
        var errores = LineaGestionFFValidator.Validar(vm);
        if (errores.Count > 0) throw new ArgumentException(string.Join(" ", errores));
    }

    private async Task<LineaGestion> LineaAsync(int lineaId, CancellationToken ct)
        => await lineas.GetByIdAsync(lineaId, ct)
           ?? throw new ArgumentException("La línea de gestión elegida ya no existe. Recargá la página.");

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    internal static LineaGestionFFViewModel ToVm(LineaGestionFF e) => new()
    {
        Id = e.Id,
        LineaGestionId = e.LineaGestionId,
        LineaGestionNombre = e.LineaGestion?.Nombre,
        Nombre = e.Nombre ?? string.Empty,
        Fuente = e.Fuente,
        Tipo = e.LineaGestion?.Tipo,
        Grupo = e.LineaGestion?.Grupo,
        Reparticion = e.LineaGestion?.Reparticion,
    };
}
