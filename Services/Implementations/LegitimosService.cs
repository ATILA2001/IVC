#nullable enable
using IVC.Application.Common;
using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using static IVC.Services.Implementations.RedeterminacionesMapeo;

namespace IVC.Services.Implementations;

/// <summary>
/// Legítimos abonos (ex LegitimoNegocioEF). Cada fila lleva el SIGAF total de su expediente
/// (sin prorrateo: R4, los legítimos no participan del reparto) y el último pase SADE. El
/// administrador ve todo; el resto, las obras de sus áreas.
/// </summary>
public class LegitimosService(
    ILegitimoRepository repo,
    IObraRepository obras,
    IDatosExternosService externos,
    IUsuarioActual usuarioActual,
    IPermisosIvc permisos,
    ILogger<LegitimosService> logger) : ILegitimosService
{
    public async Task<IReadOnlyList<LegitimoViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        var areaIds = AreasVisibles(usuario, usuario.EsAdmin);
        if (areaIds is { Count: 0 }) return Array.Empty<LegitimoViewModel>();

        var legitimos = await repo.GetAllAsync(areaIds, ct);
        return await ArmarFilasAsync(legitimos, ct);
    }

    public async Task<LegitimoViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var legitimo = await repo.GetByIdAsync(id, ct);
        return legitimo is null ? null : (await ArmarFilasAsync([legitimo], ct))[0];
    }

    public async Task<LegitimoViewModel> CreateAsync(LegitimoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ExigirPlanificacionAbiertaAsync(ct);
        await ExigirObraDelUsuarioAsync(vm.ObraId, ct);

        var entity = Aplicar(vm, new Legitimo { ObraId = vm.ObraId });
        try
        {
            await repo.AddAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo crear el legítimo de la obra {ObraId}.", vm.ObraId);
            throw ErroresSql.Traducir(ex);
        }
        return await GetByIdAsync(entity.Id, ct) ?? (await ArmarFilasAsync([entity], ct))[0];
    }

    public async Task UpdateAsync(LegitimoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ExigirPlanificacionAbiertaAsync(ct);
        var actual = await ObtenerActualAsync(vm.Id, ct);
        // La obra no se cambia desde la edición (el legado deshabilitaba el desplegable).
        var entity = Aplicar(vm, new Legitimo { Id = actual.Id, ObraId = actual.ObraId, PorcEjecFisica = actual.PorcEjecFisica });
        await GuardarAsync(entity, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await ExigirPlanificacionAbiertaAsync(ct);
        try
        {
            await repo.DeleteAsync(id, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo eliminar el legítimo {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: el legítimo está relacionado con otros datos.");
        }
    }

    public async Task ActualizarInlineAsync(LegitimoViewModel vm, CancellationToken ct = default)
    {
        if (!await permisos.PuedeEditarInlineAsync(ct))
            throw new ArgumentException("No tenés permiso para editar legítimos.");
        if (ErrorDeValidacion(LegitimoValidator.ValidarInline(vm)) is string error)
            throw new ArgumentException(error);

        var actual = await ObtenerActualAsync(vm.Id, ct);
        var entity = new Legitimo
        {
            Id = actual.Id,
            ObraId = actual.ObraId,
            CodigoAutorizante = actual.CodigoAutorizante,
            Expediente = vm.Expediente?.Trim() ?? string.Empty,
            InicioEjecucion = actual.InicioEjecucion,
            FinEjecucion = actual.FinEjecucion,
            Certificado = vm.Certificado,
            MesAprobacion = actual.MesAprobacion,
            PorcEjecFisica = actual.PorcEjecFisica,
        };
        await GuardarAsync(entity, ct);
    }

    private async Task<List<LegitimoViewModel>> ArmarFilasAsync(IReadOnlyList<Legitimo> legitimos, CancellationToken ct)
    {
        if (legitimos.Count == 0) return new List<LegitimoViewModel>();
        var expedientes = Expedientes(legitimos.Select(l => l.Expediente));
        var devengados = await externos.GetDevengadosAsync(expedientes, ct);
        var sade = await externos.GetUltimosPasesSadeAsync(expedientes, ct);
        return legitimos.Select(l => ToVm(l, devengados, sade)).ToList();
    }

    private static void Validar(LegitimoViewModel vm)
    {
        if (ErrorDeValidacion(LegitimoValidator.Validar(vm)) is string error)
            throw new ArgumentException(error);
    }

    private async Task ExigirPlanificacionAbiertaAsync(CancellationToken ct)
    {
        if (!await permisos.PuedeModificarPlanificacionAsync(ct))
            throw new ArgumentException("La planificación está cerrada: no se pueden crear, modificar ni eliminar legítimos.");
    }

    private async Task ExigirObraDelUsuarioAsync(int obraId, CancellationToken ct)
    {
        var obra = await obras.GetByIdAsync(obraId, ct) ?? throw new ArgumentException("La obra elegida no existe.");
        var usuario = await usuarioActual.ObtenerAsync(ct);
        if (!usuario.EsAdmin && !usuario.IvcAreaIds.Contains(obra.AreaId))
            throw new ArgumentException("Sólo podés dar de alta legítimos en obras de tu propia área.");
    }

    private async Task<Legitimo> ObtenerActualAsync(int id, CancellationToken ct)
    {
        var actual = await repo.GetByIdAsync(id, ct);
        if (actual is not null) return actual;
        logger.LogWarning("Legítimo {Id} ya no existe; no se guardaron los cambios.", id);
        throw new ConflictoDeConcurrenciaException(
            "Otro usuario eliminó este legítimo mientras lo editabas. Se recargaron los datos.");
    }

    private async Task GuardarAsync(Legitimo entity, CancellationToken ct)
    {
        try
        {
            await repo.UpdateAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo guardar el legítimo {Id}.", entity.Id);
            throw ErroresSql.Traducir(ex);
        }
    }

    private static Legitimo Aplicar(LegitimoViewModel vm, Legitimo e)
    {
        e.CodigoAutorizante = vm.CodigoAutorizante?.Trim() ?? string.Empty;
        e.Expediente = vm.Expediente?.Trim() ?? string.Empty;
        e.InicioEjecucion = vm.InicioEjecucion;
        e.FinEjecucion = vm.FinEjecucion;
        e.Certificado = vm.Certificado;
        e.MesAprobacion = vm.MesAprobacion;
        return e;
    }

    internal static LegitimoViewModel ToVm(Legitimo l, IReadOnlyDictionary<string, decimal> devengados, IReadOnlyDictionary<string, UltimoPaseSade> sade)
    {
        // SIGAF total del expediente, sin prorrateo (R4).
        decimal? sigaf = !string.IsNullOrWhiteSpace(l.Expediente) && devengados.TryGetValue(l.Expediente.Trim(), out var total) ? total : null;
        var (estado, buzon, fecha) = PropiedadesPago.Calcular(l.Expediente, sigaf, sade);
        return new LegitimoViewModel
        {
            Id = l.Id,
            ObraId = l.ObraId ?? 0,
            Area = l.Obra?.Area?.Nombre,
            Obra = l.Obra?.Descripcion,
            Barrio = l.Obra?.Barrio?.Nombre,
            Empresa = l.Obra?.Empresa?.Nombre,
            LineaGestion = l.Obra?.Proyecto?.LineaGestion?.Nombre,
            CodigoAutorizante = l.CodigoAutorizante,
            Expediente = l.Expediente,
            InicioEjecucion = l.InicioEjecucion,
            FinEjecucion = l.FinEjecucion,
            Certificado = l.Certificado,
            MesAprobacion = l.MesAprobacion,
            Estado = estado,
            Sigaf = sigaf,
            BuzonSade = buzon,
            FechaSade = fecha,
        };
    }
}
