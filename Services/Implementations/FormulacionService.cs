#nullable enable
using IVC.Application.Common;
using IVC.Application.Formulacion;
using IVC.Application.Formulacion.Dtos;
using IVC.Application.Identidad;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IVC.Services.Implementations;

/// <summary>
/// Formulación (ex FormulacionNegocioEF + FormulacionEF.aspx.cs). Arma el pivot de cada
/// obra con sus filas del ciclo y persiste alta y edición con la misma regla que el legado:
/// upsert por año con monto, borrado del año sin monto, campos compartidos replicados.
/// </summary>
public class FormulacionService(
    IFormulacionRepository repo,
    IObraRepository obras,
    ILookupRepository lookups,
    IUsuarioActual usuarioActual,
    IPermisosIvc permisos,
    IOptions<FormulacionOptions> opciones,
    ILogger<FormulacionService> logger) : IFormulacionService
{
    public FormulacionCiclo Ciclo { get; } = FormulacionCiclo.Desde(opciones.Value);

    public async Task<IReadOnlyList<FormulacionViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var areaIds = await AreasVisiblesAsync(ct);
        if (areaIds is { Count: 0 }) return Array.Empty<FormulacionViewModel>();

        var filas = await repo.GetDelCicloAsync(Ciclo.Anios, areaIds, ct);
        // Las filas vienen ordenadas por obra: el agrupado conserva ese orden.
        return filas.GroupBy(f => f.ObraId).Select(g => ToVm(g.Key, g.ToList(), Ciclo)).ToList();
    }

    public async Task<FormulacionViewModel?> GetPorObraAsync(int obraId, CancellationToken ct = default)
    {
        var filas = await repo.GetDeObraAsync(obraId, Ciclo.Anios, ct);
        return filas.Count == 0 ? null : ToVm(obraId, filas, Ciclo);
    }

    public async Task<FormulacionViewModel> GuardarAsync(FormulacionViewModel vm, bool esAlta, CancellationToken ct = default)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        if (FormulacionValidator.Validar(vm, esAlta, editaPpiYTecho: usuario.EsAdmin) is { Count: > 0 } errores)
            throw new ArgumentException(string.Join(" ", errores));
        await ExigirFormulacionAbiertaAsync(ct);
        await ExigirObraDelUsuarioAsync(vm.ObraId, usuario, ct);

        var existentes = await repo.GetDeObraAsync(vm.ObraId, Ciclo.Anios, ct);
        if (esAlta && existentes.Count > 0)
            throw new ArgumentException("La obra ya tiene formulación en el ciclo vigente: editala desde su fila.");
        if (!esAlta && existentes.Count == 0)
        {
            logger.LogWarning("La formulación de la obra {ObraId} ya no existe; no se guardaron los cambios.", vm.ObraId);
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó la formulación de esta obra mientras la editabas. Se recargaron los datos.");
        }

        // El PPI y el techo los carga sólo el administrador (el panel oculto del legado): el
        // resto conserva los que ya estaban (en el alta quedan vacíos).
        var existente = existentes.FirstOrDefault();
        var compartido = usuario.EsAdmin
            ? Compartido(vm, vm.Ppi, vm.Techos)
            : Compartido(vm, existente?.Ppi, existente?.Techos);
        try
        {
            await repo.GuardarPivotAsync(vm.ObraId, Ciclo.Anios, compartido, vm.Montos, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo guardar la formulación de la obra {ObraId}.", vm.ObraId);
            throw ErroresSql.Traducir(ex);
        }
        return await GetPorObraAsync(vm.ObraId, ct)
            ?? throw new InvalidOperationException("La formulación se guardó pero no se pudo releer.");
    }

    public async Task EliminarAsync(int obraId, CancellationToken ct = default)
    {
        await ExigirFormulacionAbiertaAsync(ct);
        var usuario = await usuarioActual.ObtenerAsync(ct);
        await ExigirObraDelUsuarioAsync(obraId, usuario, ct);
        if (!await repo.EliminarDeObraAsync(obraId, Ciclo.Anios, ct))
            throw new ConflictoDeConcurrenciaException(
                "La formulación de esta obra ya no existe. Se recargaron los datos.");
    }

    public async Task<IReadOnlyList<OpcionLookup>> GetObrasDisponiblesAsync(CancellationToken ct = default)
    {
        var areaIds = await AreasVisiblesAsync(ct);
        if (areaIds is { Count: 0 }) return Array.Empty<OpcionLookup>();
        return await lookups.GetObrasSinFormulacionAsync(Ciclo.Anios, areaIds, ct);
    }

    /// <summary>null = todas (administrador); si no, las áreas del usuario (vacío = no ve nada).</summary>
    private async Task<IReadOnlyCollection<int>?> AreasVisiblesAsync(CancellationToken ct)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        return usuario.EsAdmin ? null : ReglasPermisosIvc.AreaIdsParaFiltro(usuario);
    }

    /// <summary>
    /// Alta, modificación y baja sólo con la formulación abierta (o administrador); nunca
    /// Redeterminaciones ni Secretaría. La UI esconde los botones y acá se vuelve a verificar.
    /// </summary>
    private async Task ExigirFormulacionAbiertaAsync(CancellationToken ct)
    {
        if (!await permisos.PuedeModificarFormulacionAsync(ct))
            throw new ArgumentException("La formulación está cerrada: no se pueden crear, modificar ni eliminar formulaciones.");
    }

    private async Task ExigirObraDelUsuarioAsync(int obraId, UsuarioIvc usuario, CancellationToken ct)
    {
        var obra = await obras.GetByIdAsync(obraId, ct) ?? throw new ArgumentException("La obra elegida no existe.");
        if (!usuario.EsAdmin && !usuario.IvcAreaIds.Contains(obra.AreaId))
            throw new ArgumentException("Sólo podés formular obras de tu propia área.");
    }

    /// <summary>Campos compartidos que se replican en las (hasta) tres filas de la obra.</summary>
    internal static Formulacion Compartido(FormulacionViewModel vm, int? ppi, decimal? techos) => new()
    {
        MesBase = vm.MesBase,
        Observaciones = Texto(vm.Observaciones),
        Ppi = ppi,
        Techos = techos,
        UnidadMedidaId = vm.UnidadMedidaId,
        ValorMedida = vm.ValorMedida,
        PrioridadId = vm.PrioridadId,
        BreveDescripcion = Texto(vm.BreveDescripcion),
        FechaInicio = vm.FechaInicio,
        FechaFin = vm.FechaFin,
    };

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Pivot de una obra (ex BuildPivot): los campos compartidos se toman de la fila más
    /// antigua (son idénticos) y los montos se ubican por año en las posiciones del ciclo.
    /// </summary>
    internal static FormulacionViewModel ToVm(int obraId, IReadOnlyList<Formulacion> filas, FormulacionCiclo ciclo)
    {
        var primera = filas.OrderBy(f => f.FechaPeriodo).First();
        var obra = primera.Obra;
        var vm = new FormulacionViewModel
        {
            ObraId = obraId,
            Area = obra?.Area?.Nombre,
            Empresa = obra?.Empresa?.Nombre,
            Contrata = RedeterminacionesMapeo.TextoContrata(obra),
            Barrio = obra?.Barrio?.Nombre,
            LineaGestion = obra?.Proyecto?.LineaGestion?.Nombre,
            Proyecto = obra?.Proyecto?.Nombre,
            Obra = obra?.Descripcion,
            BreveDescripcion = primera.BreveDescripcion,
            FechaInicio = primera.FechaInicio,
            FechaFin = primera.FechaFin,
            Ppi = primera.Ppi,
            Techos = primera.Techos,
            MesBase = primera.MesBase,
            UnidadMedidaId = primera.UnidadMedidaId,
            UnidadMedida = primera.UnidadMedida?.Nombre,
            ValorMedida = primera.ValorMedida,
            Observaciones = primera.Observaciones,
            PrioridadId = primera.PrioridadId,
            Prioridad = primera.Prioridad?.Nombre,
        };
        foreach (var fila in filas)
        {
            switch (ciclo.Posicion(fila.FechaPeriodo.Year))
            {
                case 0: vm.MontoAnio1 = fila.Monto; break;
                case 1: vm.MontoAnio2 = fila.Monto; break;
                case 2: vm.MontoAnio3 = fila.Monto; break;
            }
        }
        return vm;
    }
}
