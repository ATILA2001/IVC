#nullable enable
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Obras;
using IVC.Application.Obras.Dtos;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IVC.Services.Implementations;

/// <summary>
/// Obras (ex ObraNegocioEF + CalculoObraNegocioEF). Las reglas del usuario se aplican
/// acá, no en la página: visibilidad por área, área del alta y planificación abierta.
/// Los indicadores se calculan con <see cref="IndicadoresObra"/> sobre los agregados
/// del repositorio, sin cambios de fórmula respecto del legado.
/// </summary>
public class ObrasService(
    IObraRepository repo,
    ILookupRepository lookups,
    IUsuarioActual usuarioActual,
    IPermisosIvc permisos,
    ILogger<ObrasService> logger) : IObrasService
{
    public async Task<IReadOnlyList<ObraViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        // Admin y Redeterminaciones ven todas las obras; el resto, las de sus áreas
        // (sin áreas resueltas no ve ninguna: fail-closed, como el legado).
        IReadOnlyCollection<int>? areaIds = ReglasPermisosIvc.VeTodasLasObras(usuario)
            ? null
            : ReglasPermisosIvc.AreaIdsParaFiltro(usuario);
        if (areaIds is { Count: 0 }) return Array.Empty<ObraViewModel>();

        var obras = await repo.GetAllAsync(areaIds, ct);
        if (obras.Count == 0) return Array.Empty<ObraViewModel>();
        var agregados = await repo.GetAgregadosAsync(areaIds is null ? null : obras.Select(o => o.Id).ToList(), ct);
        return obras.Select(o => ToVm(o, agregados)).ToList();
    }

    public async Task<ObraViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var obra = await repo.GetByIdAsync(id, ct);
        if (obra is null) return null;
        var agregados = await repo.GetAgregadosAsync([id], ct);
        return ToVm(obra, agregados);
    }

    public async Task<ObraViewModel> CreateAsync(ObraViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ExigirPlanificacionAbiertaAsync(ct);
        var usuario = await usuarioActual.ObtenerAsync(ct);
        if (!usuario.EsAdmin && !usuario.IvcAreaIds.Contains(vm.AreaId))
            throw new ArgumentException("Sólo podés dar de alta obras en tu propia área.");

        var entity = Aplicar(vm, new Obra { AreaId = vm.AreaId });
        await repo.AddAsync(entity, ct);
        return await GetByIdAsync(entity.Id, ct) ?? ToVm(entity, new Dictionary<int, AgregadosObra>());
    }

    public async Task UpdateAsync(ObraViewModel vm, CancellationToken ct = default)
    {
        Validar(vm);
        await ExigirPlanificacionAbiertaAsync(ct);
        var actual = await repo.GetByIdAsync(vm.Id, ct);
        if (actual is null)
        {
            logger.LogWarning("Obra {Id} ya no existe; no se guardaron los cambios.", vm.Id);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó esta obra mientras la editabas. Se recargaron los datos.");
        }
        // El área no se cambia desde la edición (el legado deshabilitaba el desplegable).
        // Entidad sin navegaciones: Update marcaría como modificado todo lo cargado por Include.
        var entity = Aplicar(vm, new Obra { Id = actual.Id, AreaId = actual.AreaId });
        await repo.UpdateAsync(entity, ct);
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
            logger.LogWarning(ex, "No se pudo eliminar la obra {Id} (en uso).", id);
            throw new ArgumentException(
                "No se puede eliminar: la obra tiene autorizantes, legítimos, formulación, proyecto o movimientos asociados.");
        }
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetAreasPermitidasAsync(CancellationToken ct = default)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        var areas = await lookups.GetAreasAsync(ct);
        return usuario.EsAdmin ? areas : areas.Where(a => usuario.IvcAreaIds.Contains(a.Id)).ToList();
    }

    private static void Validar(ObraViewModel vm)
    {
        var errores = ObraValidator.Validar(vm);
        if (errores.Count > 0) throw new ArgumentException(string.Join(" ", errores));
    }

    /// <summary>
    /// Alta, modificación y baja sólo con la planificación abierta (o administrador, o
    /// habilitación propia): la UI esconde los botones y acá se vuelve a verificar.
    /// </summary>
    private async Task ExigirPlanificacionAbiertaAsync(CancellationToken ct)
    {
        if (!await permisos.PuedeModificarPlanificacionAsync(ct))
            throw new ArgumentException("La planificación está cerrada: no se pueden crear, modificar ni eliminar obras.");
    }

    private static Obra Aplicar(ObraViewModel vm, Obra e)
    {
        e.Descripcion = vm.Descripcion.Trim();
        e.Numero = vm.Numero;
        e.Anio = vm.Anio;
        e.Etapa = vm.Etapa;
        e.ObraNumero = vm.ObraNumero;
        e.EmpresaId = vm.EmpresaId;
        e.ContrataId = vm.ContrataId;
        e.BarrioId = vm.BarrioId;
        return e;
    }

    internal static ObraViewModel ToVm(Obra o, IReadOnlyDictionary<int, AgregadosObra> agregados)
    {
        var indicadores = IndicadoresObra.Calcular(
            agregados.TryGetValue(o.Id, out var a) ? a : AgregadosObra.Vacio,
            o.Proyecto?.Autorizado2026);
        return new ObraViewModel
        {
            Id = o.Id,
            AreaId = o.AreaId,
            Area = o.Area?.Nombre,
            EmpresaId = o.EmpresaId,
            Empresa = o.Empresa?.Nombre,
            ContrataId = o.ContrataId,
            Contrata = o.Contrata?.Nombre,
            Numero = o.Numero,
            Anio = o.Anio,
            BarrioId = o.BarrioId,
            Barrio = o.Barrio?.Nombre,
            Descripcion = o.Descripcion ?? string.Empty,
            Etapa = o.Etapa,
            ObraNumero = o.ObraNumero,
            LineaGestionNombre = o.Proyecto?.LineaGestion?.Nombre,
            ProyectoNombre = o.Proyecto?.Nombre,
            Autorizado2026 = o.Proyecto?.Autorizado2026,
            MontoCertificado = indicadores.MontoCertificado,
            Porcentaje = indicadores.Porcentaje,
            MontoInicial = indicadores.MontoInicial,
            MontoActual = indicadores.MontoActual,
            MontoFaltante = indicadores.MontoFaltante,
            FechaInicio = indicadores.FechaInicio,
            FechaFin = indicadores.FechaFin,
            EjecFisica = indicadores.EjecFisica,
            FaltanteEjecFisica = indicadores.FaltanteEjecFisica,
        };
    }
}
