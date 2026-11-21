#nullable enable
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using static IVC.Services.Implementations.RedeterminacionesMapeo;

namespace IVC.Services.Implementations;

/// <summary>
/// Autorizantes (ex AutorizanteNegocioEF + ListarAutorizantesYRedeterminaciones). La grilla
/// mezcla los autorizantes reales con una fila virtual por redeterminación, que muestra el
/// monto redeterminado calculado por <see cref="ReglasRedeterminaciones"/> y el estado
/// general de su etapa. Admin y Redeterminaciones ven todo; el resto, sus áreas.
/// </summary>
public class AutorizantesService(
    IAutorizanteRepository repo,
    IRedeterminacionRepository redeterminaciones,
    ICertificadoRepository certificados,
    IObraRepository obras,
    IDatosExternosService externos,
    IUsuarioActual usuarioActual,
    IPermisosIvc permisos,
    ILogger<AutorizantesService> logger) : IAutorizantesService
{
    /// <summary>Concepto con el que el legado etiquetaba las filas virtuales (CONCEPTOS 11 = REDETERMINACION).</summary>
    public const int ConceptoRedeterminacionId = 11;
    public const string ConceptoRedeterminacionNombre = "REDETERMINACION";

    public async Task<IReadOnlyList<AutorizanteViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        var areaIds = AreasVisibles(usuario, ReglasPermisosIvc.VeTodasLasObras(usuario));
        if (areaIds is { Count: 0 }) return Array.Empty<AutorizanteViewModel>();

        var autorizantes = await repo.GetAllAsync(areaIds, ct);
        if (autorizantes.Count == 0) return Array.Empty<AutorizanteViewModel>();
        return await ArmarFilasAsync(autorizantes, ct);
    }

    public async Task<AutorizanteViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var autorizante = await repo.GetByIdAsync(id, ct);
        if (autorizante is null) return null;
        var filas = await ArmarFilasAsync([autorizante], ct);
        return filas.First(f => !f.EsRedeterminacion);
    }

    public async Task<AutorizanteViewModel> CreateAsync(AutorizanteViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: true);
        await ExigirPlanificacionAbiertaAsync(ct);
        await ExigirObraDelUsuarioAsync(vm.ObraId, ct);

        // Sin código: lo arma el trigger TRG_Autorizantes_Insert (OBRA-n). AUTORIZACION_GG en
        // true como el default de la columna (el legado no la enviaba).
        var entity = Aplicar(vm, new Autorizante { ObraId = vm.ObraId, AutorizacionGG = true });
        try
        {
            await repo.AddAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo crear el autorizante de la obra {ObraId}.", vm.ObraId);
            throw ErroresSql.Traducir(ex);
        }
        return await GetByIdAsync(entity.Id, ct) ?? ToVm(entity, SinSade, SinAcdir, SinImportes);
    }

    public async Task UpdateAsync(AutorizanteViewModel vm, CancellationToken ct = default)
    {
        ExigirReal(vm);
        Validar(vm, esAlta: false);
        await ExigirPlanificacionAbiertaAsync(ct);
        var actual = await ObtenerActualAsync(vm.Id, ct);

        // Misma regla que el trigger trg_Autorizante_CheckMonto, con mensaje propio.
        var certificado = await repo.SumaCertificadosAsync(actual.CodigoAutorizante ?? string.Empty, ct);
        if (vm.MontoAutorizado < certificado)
            throw new ArgumentException(
                $"El monto autorizado no puede ser menor que la suma de los certificados ya cargados ({certificado:N2}).");

        // La obra y el código no se cambian desde la edición. Entidad sin navegaciones.
        var entity = Aplicar(vm, new Autorizante
        {
            Id = actual.Id,
            ObraId = actual.ObraId,
            CodigoAutorizante = actual.CodigoAutorizante,
            AutorizacionGG = actual.AutorizacionGG,
        });
        try
        {
            await repo.UpdateAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo guardar el autorizante {Id}.", vm.Id);
            throw ErroresSql.Traducir(ex);
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await ExigirPlanificacionAbiertaAsync(ct);
        var actual = await repo.GetByIdAsync(id, ct);
        if (actual is null) return;
        if (actual.CodigoAutorizante is not null && await repo.TieneDependenciasAsync(actual.CodigoAutorizante, ct))
            throw new ArgumentException("No se puede eliminar: el autorizante tiene certificados o redeterminaciones asociados.");
        try
        {
            await repo.DeleteAsync(id, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo eliminar el autorizante {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: el autorizante está relacionado con otros datos.");
        }
    }

    public async Task ActualizarSeguimientoAsync(AutorizanteViewModel vm, CancellationToken ct = default)
    {
        ExigirReal(vm);
        if (!await permisos.PuedeEditarInlineAsync(ct))
            throw new ArgumentException("No tenés permiso para editar autorizantes.");
        if (ErrorDeValidacion(AutorizanteValidator.ValidarSeguimiento(vm)) is string error)
            throw new ArgumentException(error);

        if (!await repo.UpdateSeguimientoAsync(vm.Id, vm.Expediente?.Trim(), vm.EstadoId, ct))
            throw new ConflictoDeConcurrenciaException(
                "Otro usuario eliminó este autorizante mientras lo editabas. Se recargaron los datos.");
    }

    /// <summary>Filas reales más las virtuales de sus redeterminaciones, con SADE, ACDIR y ejecución física.</summary>
    private async Task<List<AutorizanteViewModel>> ArmarFilasAsync(IReadOnlyList<Autorizante> autorizantes, CancellationToken ct)
    {
        var porCodigo = autorizantes
            .Where(a => !string.IsNullOrEmpty(a.CodigoAutorizante))
            .GroupBy(a => a.CodigoAutorizante!)
            .ToDictionary(g => g.Key, g => g.First());
        var codigos = porCodigo.Keys.ToList();

        var redets = await redeterminaciones.GetPorCodigosAsync(codigos, ct);
        var ejecFisica = await certificados.GetEjecFisicaPorCodigoAsync(codigos, ct);
        // Los certificados sólo hacen falta para calcular el monto de las redeterminaciones (R1, R5).
        var codigosConRedet = redets.Select(r => r.CodigoAutorizante).OfType<string>().Distinct().ToList();
        IReadOnlyList<Certificado> certs = codigosConRedet.Count == 0
            ? Array.Empty<Certificado>()
            : await certificados.GetPorCodigosAsync(codigosConRedet, ct);
        var calculos = CalcularMontos(redets, autorizantes,
            certs.Select(c => ToCalculo(c, porCodigo.TryGetValue(c.CodigoAutorizante, out var a) ? a.MontoAutorizado : null)));

        var expedientes = Expedientes(autorizantes.Select(a => a.Expediente).Concat(redets.Select(r => r.Expediente)));
        var sade = await externos.GetUltimosPasesSadeAsync(expedientes, ct);
        var acdir = await externos.GetAcdirsAsync(expedientes, ct);

        var filas = autorizantes.Select(a => ToVm(a, sade, acdir, ejecFisica)).ToList();
        filas.AddRange(redets.Select(r => ToVm(r, calculos[r.Id],
            r.CodigoAutorizante is not null && porCodigo.TryGetValue(r.CodigoAutorizante, out var a) ? a : null, sade, acdir)));
        return filas;
    }

    private static void Validar(AutorizanteViewModel vm, bool esAlta)
    {
        if (ErrorDeValidacion(AutorizanteValidator.Validar(vm, esAlta)) is string error)
            throw new ArgumentException(error);
    }

    private static void ExigirReal(AutorizanteViewModel vm)
    {
        if (vm.EsRedeterminacion)
            throw new ArgumentException("Las redeterminaciones se editan desde su propia página.");
    }

    private async Task ExigirPlanificacionAbiertaAsync(CancellationToken ct)
    {
        if (!await permisos.PuedeModificarPlanificacionAsync(ct))
            throw new ArgumentException("La planificación está cerrada: no se pueden crear, modificar ni eliminar autorizantes.");
    }

    /// <summary>El alta sólo sobre obras de las áreas del usuario (el administrador, sobre cualquiera).</summary>
    private async Task ExigirObraDelUsuarioAsync(int obraId, CancellationToken ct)
    {
        var obra = await obras.GetByIdAsync(obraId, ct) ?? throw new ArgumentException("La obra elegida no existe.");
        var usuario = await usuarioActual.ObtenerAsync(ct);
        if (!usuario.EsAdmin && !usuario.IvcAreaIds.Contains(obra.AreaId))
            throw new ArgumentException("Sólo podés dar de alta autorizantes en obras de tu propia área.");
    }

    private async Task<Autorizante> ObtenerActualAsync(int id, CancellationToken ct)
    {
        var actual = await repo.GetByIdAsync(id, ct);
        if (actual is not null) return actual;
        logger.LogWarning("Autorizante {Id} ya no existe; no se guardaron los cambios.", id);
        throw new ConflictoDeConcurrenciaException(
            "Otro usuario eliminó este autorizante mientras lo editabas. Se recargaron los datos.");
    }

    private static Autorizante Aplicar(AutorizanteViewModel vm, Autorizante e)
    {
        e.Detalle = vm.Detalle?.Trim();
        e.Expediente = vm.Expediente?.Trim();
        e.MontoAutorizado = vm.MontoAutorizado;
        e.MesAprobacion = vm.MesAprobacion;
        e.MesBase = vm.MesBase;
        e.ConceptoId = vm.ConceptoId;
        e.EstadoId = vm.EstadoId;
        return e;
    }

    internal static AutorizanteViewModel ToVm(
        Autorizante a,
        IReadOnlyDictionary<string, UltimoPaseSade> sade,
        IReadOnlyDictionary<string, string> acdir,
        IReadOnlyDictionary<string, decimal> ejecFisica)
    {
        var (_, buzon, fecha) = PropiedadesPago.Calcular(a.Expediente, null, sade);
        decimal? ejec = a.CodigoAutorizante is not null && ejecFisica.TryGetValue(a.CodigoAutorizante, out var e) ? e : null;
        return new AutorizanteViewModel
        {
            Id = a.Id,
            ObraId = a.ObraId ?? 0,
            Area = a.Obra?.Area?.Nombre,
            Obra = a.Obra?.Descripcion,
            Contrata = TextoContrata(a.Obra),
            Empresa = a.Obra?.Empresa?.Nombre,
            Barrio = a.Obra?.Barrio?.Nombre,
            CodigoAutorizante = a.CodigoAutorizante,
            Expediente = a.Expediente,
            Detalle = a.Detalle,
            ConceptoId = a.ConceptoId,
            Concepto = a.Concepto?.Nombre,
            MontoAutorizado = a.MontoAutorizado,
            MesAprobacion = a.MesAprobacion,
            MesBase = a.MesBase,
            EstadoId = a.EstadoId,
            Estado = a.Estado?.Nombre,
            BuzonSade = buzon,
            FechaSade = fecha,
            Acdir = acdir.TryGetValue((a.Expediente ?? string.Empty).Trim(), out var numero) ? numero : null,
            EjecFisica = ejec,
            FaltanteEjecFisica = ejec is decimal x ? 100m - x : null,
        };
    }

    /// <summary>Fila virtual de una redeterminación: código de redeterminación, estado general de su etapa y monto redeterminado.</summary>
    internal static AutorizanteViewModel ToVm(
        Redeterminacion r,
        RedeterminacionCalculo calculo,
        Autorizante? autorizante,
        IReadOnlyDictionary<string, UltimoPaseSade> sade,
        IReadOnlyDictionary<string, string> acdir)
    {
        var estado = MapeoEstadoRedet.MapearEstadoDesdeId(r.EtapaId);
        var obra = autorizante?.Obra;
        var (_, buzon, fecha) = PropiedadesPago.Calcular(r.Expediente, null, sade);
        return new AutorizanteViewModel
        {
            Id = 0,
            RedeterminacionId = r.Id,
            ObraId = obra?.Id ?? 0,
            Area = obra?.Area?.Nombre,
            Obra = obra?.Descripcion,
            Contrata = TextoContrata(obra),
            Empresa = obra?.Empresa?.Nombre,
            Barrio = obra?.Barrio?.Nombre,
            CodigoAutorizante = r.CodigoRedet,
            Expediente = r.Expediente,
            Detalle = $"{r.Tipo} - {r.Etapa?.Nombre}",
            ConceptoId = ConceptoRedeterminacionId,
            Concepto = ConceptoRedeterminacionNombre,
            MontoAutorizado = calculo.MontoRedet ?? 0,
            MesAprobacion = r.Salto,
            MesBase = null,
            EstadoId = estado.Id,
            Estado = estado.Nombre,
            BuzonSade = buzon,
            FechaSade = fecha,
            Acdir = acdir.TryGetValue((r.Expediente ?? string.Empty).Trim(), out var numero) ? numero : null,
        };
    }
}
