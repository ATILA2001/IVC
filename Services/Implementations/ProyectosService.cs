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
/// BD_PROYECTOS: una fila por obra. El alta elige la obra (sólo las que no tienen
/// proyecto); la edición toca proyecto, subproyecto, línea y autorizado 2026. El
/// autorizado 2025 es histórico y no se edita desde la página, como en el legado.
/// </summary>
public class ProyectosService(
    IProyectoRepository repo,
    ILineaGestionRepository lineas,
    ILogger<ProyectosService> logger) : IProyectosService
{
    public async Task<IReadOnlyList<ProyectoViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var filas = await repo.GetAllAsync(ct);
        return filas.Select(ToVm).ToList();
    }

    public async Task<ProyectoViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var fila = await repo.GetByIdAsync(id, ct);
        return fila is null ? null : ToVm(fila);
    }

    public async Task<ProyectoViewModel> CreateAsync(ProyectoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: true);
        if (await repo.ExisteParaObraAsync(vm.ObraId, ct))
            throw new ArgumentException("La obra ya tiene un proyecto: editá el existente.");
        await VerificarLineaAsync(vm.LineaGestionId!.Value, ct);

        var entity = new Proyecto
        {
            ObraId = vm.ObraId,
            Nombre = vm.Nombre.Trim(),
            SubProyecto = vm.SubProyecto?.Trim() ?? string.Empty,
            LineaGestionId = vm.LineaGestionId,
            Autorizado2026 = vm.Autorizado2026,
        };
        await repo.AddAsync(entity, ct);
        // Releída con la obra y sus catálogos: la fila en pantalla necesita área,
        // contrata y empresa, que el alta no conoce.
        return await GetByIdAsync(entity.Id, ct) ?? ToVm(entity);
    }

    public async Task UpdateAsync(ProyectoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: false);
        await VerificarLineaAsync(vm.LineaGestionId!.Value, ct);
        var actual = await repo.GetByIdAsync(vm.Id, ct);
        if (actual is null)
        {
            logger.LogWarning("Proyecto {Id} ya no existe; no se guardaron los cambios.", vm.Id);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó este proyecto mientras lo editabas. Se recargaron los datos.");
        }
        // Entidad sin navegaciones (ver LineasGestionFFService.UpdateAsync). La obra y
        // el autorizado 2025 se conservan tal cual están en la base.
        var entity = new Proyecto
        {
            Id = actual.Id,
            ObraId = actual.ObraId,
            Nombre = vm.Nombre.Trim(),
            SubProyecto = vm.SubProyecto?.Trim() ?? string.Empty,
            LineaGestionId = vm.LineaGestionId,
            Autorizado2025 = actual.Autorizado2025,
            Autorizado2026 = vm.Autorizado2026,
        };
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
            logger.LogWarning(ex, "No se pudo eliminar el proyecto {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: el proyecto está en uso en otros registros.");
        }
    }

    private static void Validar(ProyectoViewModel vm, bool esAlta)
    {
        var errores = ProyectoValidator.Validar(vm, esAlta);
        if (errores.Count > 0) throw new ArgumentException(string.Join(" ", errores));
    }

    private async Task VerificarLineaAsync(int lineaId, CancellationToken ct)
    {
        if (await lineas.GetByIdAsync(lineaId, ct) is null)
            throw new ArgumentException("La línea de gestión elegida ya no existe. Recargá la página.");
    }

    internal static ProyectoViewModel ToVm(Proyecto p) => new()
    {
        Id = p.Id,
        ObraId = p.ObraId,
        Area = p.Obra?.Area?.Nombre,
        Contrata = TextoContrata(p.Obra),
        Obra = TextoObra(p.Obra),
        Empresa = p.Obra?.Empresa?.Nombre,
        Nombre = p.Nombre ?? string.Empty,
        SubProyecto = p.SubProyecto,
        LineaGestionId = p.LineaGestionId,
        LineaGestionNombre = p.LineaGestion?.Nombre,
        Autorizado2025 = p.Autorizado2025,
        Autorizado2026 = p.Autorizado2026,
    };

    /// <summary>Como el legado: "contrata número/año".</summary>
    internal static string? TextoContrata(Obra? o)
        => o is null ? null : $"{o.Contrata?.Nombre} {o.Numero}/{o.Anio}".Trim();

    /// <summary>Como el legado: "descripción - barrio".</summary>
    internal static string? TextoObra(Obra? o)
        => o is null ? null
         : o.Barrio?.Nombre is { Length: > 0 } barrio ? $"{o.Descripcion} - {barrio}"
         : o.Descripcion;
}
