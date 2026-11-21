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
/// Redeterminaciones (ex RedeterminacionNegocioEF). Cross-area: todos los usuarios ven
/// todas (decisión del 01/11/2026). Cada fila lleva los datos de obra de su autorizante, el
/// último pase SADE, los días en ese buzón (<see cref="SeguimientoBuzon"/>) y si el buzón
/// no corresponde a la etapa (<see cref="BuzonEtapaMatcher"/>). Alta, edición, baja y
/// seguimiento inline con <c>PuedeModificarRedeterminaciones</c>.
/// </summary>
public class RedeterminacionesService(
    IRedeterminacionRepository repo,
    IAutorizanteRepository autorizantes,
    IDatosExternosService externos,
    IPermisosIvc permisos,
    TimeProvider reloj,
    ILogger<RedeterminacionesService> logger) : IRedeterminacionesService
{
    public async Task<IReadOnlyList<RedeterminacionViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var redets = await repo.GetAllAsync(ct);
        return await ArmarFilasAsync(redets, ct);
    }

    public async Task<RedeterminacionViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var redet = await repo.GetByIdAsync(id, ct);
        return redet is null ? null : (await ArmarFilasAsync([redet], ct))[0];
    }

    public async Task<RedeterminacionViewModel> CreateAsync(RedeterminacionViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: true);
        await ExigirPermisoAsync(ct);
        var autorizante = await autorizantes.GetByIdAsync(vm.AutorizanteId!.Value, ct)
            ?? throw new ArgumentException("El autorizante elegido no existe.");

        // El usuario asignado se maneja desde la grilla (inline), como en el legado.
        var entity = Aplicar(vm, new Redeterminacion { CodigoAutorizante = autorizante.CodigoAutorizante });
        try
        {
            await repo.AddAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo crear la redeterminación del autorizante {Codigo}.", autorizante.CodigoAutorizante);
            throw ErroresSql.Traducir(ex);
        }
        return await GetByIdAsync(entity.Id, ct) ?? (await ArmarFilasAsync([entity], ct))[0];
    }

    public async Task UpdateAsync(RedeterminacionViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: false);
        await ExigirPermisoAsync(ct);
        var actual = await ObtenerActualAsync(vm.Id, ct);
        // El autorizante y el usuario asignado no se cambian desde la ventana. Entidad sin navegaciones.
        var entity = Aplicar(vm, new Redeterminacion
        {
            Id = actual.Id,
            CodigoAutorizante = actual.CodigoAutorizante,
            UsuarioId = actual.UsuarioId,
            Monto = actual.Monto,
        });
        try
        {
            await repo.UpdateAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo guardar la redeterminación {Id}.", vm.Id);
            throw ErroresSql.Traducir(ex);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await ExigirPermisoAsync(ct);
        try
        {
            await repo.DeleteAsync(id, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo eliminar la redeterminación {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: la redeterminación está relacionada con otros datos.");
        }
    }

    public async Task ActualizarSeguimientoAsync(RedeterminacionViewModel vm, CancellationToken ct = default)
    {
        await ExigirPermisoAsync(ct);
        if (ErrorDeValidacion(RedeterminacionValidator.ValidarSeguimiento(vm)) is string error)
            throw new ArgumentException(error);

        var guardada = await repo.UpdateSeguimientoAsync(
            vm.Id, vm.EtapaId, vm.Expediente?.Trim() ?? string.Empty, vm.UsuarioId, borrarUsuario: vm.UsuarioId is null, ct);
        if (!guardada)
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó esta redeterminación mientras la editabas. Se recargaron los datos.");
    }

    private async Task<List<RedeterminacionViewModel>> ArmarFilasAsync(IReadOnlyList<Redeterminacion> redets, CancellationToken ct)
    {
        if (redets.Count == 0) return new List<RedeterminacionViewModel>();
        var codigos = redets.Select(r => r.CodigoAutorizante).OfType<string>().Distinct().ToList();
        var porCodigo = (await autorizantes.GetPorCodigosAsync(codigos, ct))
            .Where(a => a.CodigoAutorizante is not null)
            .GroupBy(a => a.CodigoAutorizante!)
            .ToDictionary(g => g.Key, g => g.First());
        var sade = await externos.GetUltimosPasesSadeAsync(Expedientes(redets.Select(r => r.Expediente)), ct);
        var hoy = reloj.GetLocalNow().DateTime;
        return redets
            .Select(r => ToVm(r, r.CodigoAutorizante is not null && porCodigo.TryGetValue(r.CodigoAutorizante, out var a) ? a : null, sade, hoy))
            .ToList();
    }

    private static void Validar(RedeterminacionViewModel vm, bool esAlta)
    {
        if (ErrorDeValidacion(RedeterminacionValidator.Validar(vm, esAlta)) is string error)
            throw new ArgumentException(error);
    }

    private async Task ExigirPermisoAsync(CancellationToken ct)
    {
        if (!await permisos.PuedeModificarRedeterminacionesAsync(ct))
            throw new ArgumentException("No tenés permiso para modificar redeterminaciones.");
    }

    private async Task<Redeterminacion> ObtenerActualAsync(int id, CancellationToken ct)
    {
        var actual = await repo.GetByIdAsync(id, ct);
        if (actual is not null) return actual;
        logger.LogWarning("Redeterminación {Id} ya no existe; no se guardaron los cambios.", id);
        throw new ConflictoDeConcurrenciaException(
            "Otro usuario eliminó esta redeterminación mientras la editabas. Se recargaron los datos.");
    }

    private static Redeterminacion Aplicar(RedeterminacionViewModel vm, Redeterminacion e)
    {
        e.Expediente = vm.Expediente?.Trim();
        e.Salto = vm.Salto;
        e.Nro = vm.Nro;
        e.Tipo = vm.Tipo?.Trim();
        e.EtapaId = vm.EtapaId;
        e.Porcentaje = vm.Porcentaje;
        e.Observaciones = vm.Observaciones?.Trim();
        return e;
    }

    internal static RedeterminacionViewModel ToVm(Redeterminacion r, Autorizante? autorizante, IReadOnlyDictionary<string, UltimoPaseSade> sade, DateTime hoy)
    {
        var obra = autorizante?.Obra;
        var (_, buzon, fecha) = PropiedadesPago.Calcular(r.Expediente, null, sade);
        return new RedeterminacionViewModel
        {
            Id = r.Id,
            AutorizanteId = autorizante?.Id,
            CodigoRedet = r.CodigoRedet,
            CodigoAutorizante = r.CodigoAutorizante,
            Obra = obra?.Descripcion,
            Empresa = obra?.Empresa?.Nombre,
            Contrata = TextoContrata(obra),
            Area = obra?.Area?.Nombre,
            EtapaId = r.EtapaId,
            Etapa = r.Etapa?.Nombre,
            Expediente = r.Expediente,
            Tipo = r.Tipo,
            Salto = r.Salto,
            Nro = r.Nro,
            Porcentaje = r.Porcentaje,
            Observaciones = r.Observaciones,
            UsuarioId = r.UsuarioId,
            Usuario = r.Usuario?.Nombre,
            BuzonSade = buzon,
            FechaSade = fecha,
            DiasEnBuzon = SeguimientoBuzon.DiasEnBuzon(fecha, r.EtapaId, hoy),
            Discrepancia = BuzonEtapaMatcher.IsMismatch(r.Etapa?.Nombre, buzon),
        };
    }
}
