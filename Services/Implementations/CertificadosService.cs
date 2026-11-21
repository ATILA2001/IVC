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
/// Certificados (ex CertificadoNegocioEF + ListarCertificadosYReliquidaciones). La grilla
/// mezcla los certificados reales con las reliquidaciones virtuales que generan las
/// redeterminaciones (<see cref="ReglasRedeterminaciones.GenerarReliquidaciones"/>), y
/// completa cada fila con el SIGAF prorrateado entre los pagos del mismo expediente y el
/// último pase SADE. El administrador ve todo; el resto, los autorizantes de sus áreas.
/// </summary>
public class CertificadosService(
    ICertificadoRepository repo,
    IAutorizanteRepository autorizantes,
    IRedeterminacionRepository redeterminaciones,
    IExpedienteReliqRepository expedientesReliq,
    IDatosExternosService externos,
    IUsuarioActual usuarioActual,
    IPermisosIvc permisos,
    ILogger<CertificadosService> logger) : ICertificadosService
{
    public const string TipoPagoReliquidacionNombre = "RELIQUIDACION";

    public async Task<IReadOnlyList<CertificadoViewModel>> GetAllAsync(CancellationToken ct = default)
    {
        var usuario = await usuarioActual.ObtenerAsync(ct);
        var areaIds = AreasVisibles(usuario, usuario.EsAdmin);
        if (areaIds is { Count: 0 }) return Array.Empty<CertificadoViewModel>();

        var auts = await autorizantes.GetAllAsync(areaIds, ct);
        if (auts.Count == 0) return Array.Empty<CertificadoViewModel>();
        var porCodigo = auts
            .Where(a => !string.IsNullOrEmpty(a.CodigoAutorizante))
            .GroupBy(a => a.CodigoAutorizante!)
            .ToDictionary(g => g.Key, g => g.First());
        var codigos = porCodigo.Keys.ToList();

        var certs = await repo.GetPorCodigosAsync(codigos, ct);
        var redets = await redeterminaciones.GetPorCodigosAsync(codigos, ct);
        var expedientesDeReliq = (await expedientesReliq.GetAllAsync(ct))
            .GroupBy(e => ClaveReliquidacion(e.CodigoRedet, e.MesAprobacion))
            .ToDictionary(g => g.Key, g => g.Last().Expediente);

        var filas = new List<CertificadoViewModel>(certs.Count);
        foreach (var c in certs)
            if (porCodigo.TryGetValue(c.CodigoAutorizante, out var a))
                filas.Add(ToVm(c, a));

        // Reliquidaciones: cada redeterminación sobre los certificados reales de su autorizante (R5).
        var certsCalculo = certs
            .Where(c => porCodigo.ContainsKey(c.CodigoAutorizante))
            .Select(c => ToCalculo(c, porCodigo[c.CodigoAutorizante].MontoAutorizado))
            .ToList();
        var calculos = CalcularMontos(redets, auts, certsCalculo);
        var certsPorCodigo = certsCalculo.GroupBy(c => c.CodigoAutorizante).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var r in redets)
        {
            if (r.CodigoAutorizante is null || !porCodigo.TryGetValue(r.CodigoAutorizante, out var a)) continue;
            certsPorCodigo.TryGetValue(r.CodigoAutorizante, out var delAutorizante);
            foreach (var reliq in ReglasRedeterminaciones.GenerarReliquidaciones(calculos[r.Id], delAutorizante ?? []))
            {
                expedientesDeReliq.TryGetValue(ClaveReliquidacion(r.CodigoRedet ?? string.Empty, reliq.Certificado.MesAprobacion!.Value), out var expediente);
                filas.Add(ToVmReliquidacion(r, a, reliq, expediente));
            }
        }

        await CompletarExternosAsync(filas, ct);
        return filas;
    }

    public async Task<CertificadoViewModel> CreateAsync(CertificadoViewModel vm, CancellationToken ct = default)
    {
        Validar(vm, esAlta: true);
        await ExigirPlanificacionAbiertaAsync(ct);
        var autorizante = await ExigirAutorizanteDelUsuarioAsync(vm.AutorizanteId!.Value, ct);

        var entity = Aplicar(vm, new Certificado { CodigoAutorizante = autorizante.CodigoAutorizante ?? string.Empty });
        try
        {
            await repo.AddAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo crear el certificado del autorizante {Codigo}.", autorizante.CodigoAutorizante);
            throw ErroresSql.Traducir(ex);
        }
        // Una sola fila: el SIGAF se prorratea al recargar la grilla.
        var fila = ToVm(entity, autorizante);
        await CompletarExternosAsync([fila], ct);
        return fila;
    }

    public async Task UpdateAsync(CertificadoViewModel vm, CancellationToken ct = default)
    {
        ExigirReal(vm);
        Validar(vm, esAlta: false);
        await ExigirPlanificacionAbiertaAsync(ct);
        var actual = await ObtenerActualAsync(vm.Id, ct);

        // El autorizante no se cambia desde la edición (el legado tampoco); la ejecución física es inline.
        var entity = Aplicar(vm, new Certificado
        {
            Id = actual.Id,
            CodigoAutorizante = actual.CodigoAutorizante,
            PorcEjecFisica = actual.PorcEjecFisica,
        });
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
            logger.LogWarning(ex, "No se pudo eliminar el certificado {Id} (en uso).", id);
            throw new ArgumentException("No se puede eliminar: el certificado está relacionado con otros datos.");
        }
    }

    public async Task ActualizarInlineAsync(CertificadoViewModel vm, CancellationToken ct = default)
    {
        if (!await permisos.PuedeEditarInlineAsync(ct))
            throw new ArgumentException("No tenés permiso para editar certificados.");
        if (ErrorDeValidacion(CertificadoValidator.ValidarInline(vm)) is string error)
            throw new ArgumentException(error);

        if (vm.EsReliquidacion)
        {
            // La reliquidación no existe en CERTIFICADOS: su expediente vive en EXPEDIENTES_RELIQ.
            if (string.IsNullOrEmpty(vm.CodigoAutorizante) || vm.MesAprobacion is null)
                throw new ArgumentException("La reliquidación no tiene código o mes: no se puede guardar el expediente.");
            await expedientesReliq.GuardarOActualizarAsync(vm.CodigoAutorizante, vm.MesAprobacion.Value, vm.ExpedientePago?.Trim(), ct);
            return;
        }

        var actual = await ObtenerActualAsync(vm.Id, ct);
        var entity = new Certificado
        {
            Id = actual.Id,
            CodigoAutorizante = actual.CodigoAutorizante,
            ExpedientePago = vm.ExpedientePago?.Trim() ?? string.Empty,
            MontoTotal = vm.MontoTotal,
            MesAprobacion = actual.MesAprobacion,
            TipoPagoId = actual.TipoPagoId,
            PorcEjecFisica = vm.PorcEjecFisica,
        };
        await GuardarAsync(entity, ct);
    }

    /// <summary>SIGAF prorrateado, estado y último pase SADE de cada fila (ex CalcularCertificadosPorExpedientes).</summary>
    private async Task CompletarExternosAsync(List<CertificadoViewModel> filas, CancellationToken ct)
    {
        var expedientes = Expedientes(filas.Select(f => f.ExpedientePago));
        var devengados = await externos.GetDevengadosAsync(expedientes, ct);
        var sade = await externos.GetUltimosPasesSadeAsync(expedientes, ct);
        // Montos de todos los pagos (certificados y reliquidaciones) de cada expediente; R4: sin legítimos.
        // Expediente recortado y sin distinguir mayúsculas: así agrupa SQL Server (vw_PBI_Pagos).
        var montosPorExpediente = filas
            .Where(f => !string.IsNullOrWhiteSpace(f.ExpedientePago))
            .GroupBy(f => f.ExpedientePago!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<decimal>)g.Select(f => f.MontoTotal ?? 0).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            fila.Sigaf = CalcularSigaf(fila, devengados, montosPorExpediente);
            var (estado, buzon, fecha) = PropiedadesPago.Calcular(fila.ExpedientePago, fila.Sigaf, sade);
            fila.Estado = estado;
            fila.BuzonSade = buzon;
            fila.FechaSade = fecha;
        }
    }

    internal static decimal? CalcularSigaf(
        CertificadoViewModel fila,
        IReadOnlyDictionary<string, decimal> devengados,
        IReadOnlyDictionary<string, IReadOnlyCollection<decimal>> montosPorExpediente)
    {
        if (string.IsNullOrWhiteSpace(fila.ExpedientePago)) return null;
        var expediente = fila.ExpedientePago.Trim();
        if (!devengados.TryGetValue(expediente, out var total)) return null;
        montosPorExpediente.TryGetValue(expediente, out var montos);
        return ReglasRedeterminaciones.ProrratearSigaf(total, fila.MontoTotal ?? 0, montos);
    }

    private static string ClaveReliquidacion(string codigoRedet, DateTime mes) => $"{codigoRedet}|{mes:yyyyMM}";

    private static void Validar(CertificadoViewModel vm, bool esAlta)
    {
        if (ErrorDeValidacion(CertificadoValidator.Validar(vm, esAlta)) is string error)
            throw new ArgumentException(error);
    }

    private static void ExigirReal(CertificadoViewModel vm)
    {
        if (vm.EsReliquidacion)
            throw new ArgumentException("Las reliquidaciones se calculan a partir de las redeterminaciones: no se editan ni se eliminan.");
    }

    private async Task ExigirPlanificacionAbiertaAsync(CancellationToken ct)
    {
        if (!await permisos.PuedeModificarPlanificacionAsync(ct))
            throw new ArgumentException("La planificación está cerrada: no se pueden crear, modificar ni eliminar certificados.");
    }

    /// <summary>El alta sólo sobre autorizantes de obras de las áreas del usuario (el administrador, sobre cualquiera).</summary>
    private async Task<Autorizante> ExigirAutorizanteDelUsuarioAsync(int autorizanteId, CancellationToken ct)
    {
        var autorizante = await autorizantes.GetByIdAsync(autorizanteId, ct) ?? throw new ArgumentException("El autorizante elegido no existe.");
        var usuario = await usuarioActual.ObtenerAsync(ct);
        if (!usuario.EsAdmin && (autorizante.Obra is null || !usuario.IvcAreaIds.Contains(autorizante.Obra.AreaId)))
            throw new ArgumentException("Sólo podés cargar certificados de autorizantes de tu propia área.");
        return autorizante;
    }

    private async Task<Certificado> ObtenerActualAsync(int id, CancellationToken ct)
    {
        var actual = await repo.GetByIdAsync(id, ct);
        if (actual is not null) return actual;
        logger.LogWarning("Certificado {Id} ya no existe; no se guardaron los cambios.", id);
        throw new ConflictoDeConcurrenciaException(
            "Otro usuario eliminó este certificado mientras lo editabas. Se recargaron los datos.");
    }

    /// <summary>El trigger trg_Certificado_CheckMonto rechaza sumas que superen el monto autorizado: su mensaje llega al usuario.</summary>
    private async Task GuardarAsync(Certificado entity, CancellationToken ct)
    {
        try
        {
            await repo.UpdateAsync(entity, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "No se pudo guardar el certificado {Id}.", entity.Id);
            throw ErroresSql.Traducir(ex);
        }
    }

    private static Certificado Aplicar(CertificadoViewModel vm, Certificado e)
    {
        e.ExpedientePago = vm.ExpedientePago?.Trim() ?? string.Empty;
        e.MontoTotal = vm.MontoTotal;
        e.MesAprobacion = vm.MesAprobacion;
        e.TipoPagoId = vm.TipoPagoId;
        return e;
    }

    internal static CertificadoViewModel ToVm(Certificado c, Autorizante a) => new()
    {
        Id = c.Id,
        AutorizanteId = a.Id,
        Area = a.Obra?.Area?.Nombre,
        Contrata = TextoContrata(a.Obra),
        Obra = a.Obra?.Descripcion,
        Barrio = a.Obra?.Barrio?.Nombre,
        Proyecto = a.Obra?.Proyecto?.Nombre,
        Empresa = a.Obra?.Empresa?.Nombre,
        CodigoAutorizante = c.CodigoAutorizante,
        AutorizanteEstado = a.Estado?.Nombre,
        ExpedientePago = c.ExpedientePago,
        TipoPagoId = c.TipoPagoId,
        TipoPago = c.TipoPago?.Nombre,
        MontoTotal = c.MontoTotal,
        MesAprobacion = c.MesAprobacion,
        LineaGestion = a.Obra?.Proyecto?.LineaGestion?.Nombre,
        Porcentaje = ReglasRedeterminaciones.PorcentajeSobreAutorizado(c.MontoTotal ?? 0, a.MontoAutorizado ?? 0),
        PorcEjecFisica = c.PorcEjecFisica,
    };

    /// <summary>Fila virtual: los datos de obra son los del autorizante de la redeterminación, no los del certificado afectado.</summary>
    internal static CertificadoViewModel ToVmReliquidacion(Redeterminacion r, Autorizante a, ReliquidacionCalculada reliq, string? expediente) => new()
    {
        Id = 0,
        AutorizanteId = a.Id,
        Area = a.Obra?.Area?.Nombre,
        Contrata = TextoContrata(a.Obra),
        Obra = a.Obra?.Descripcion,
        Barrio = a.Obra?.Barrio?.Nombre,
        Proyecto = a.Obra?.Proyecto?.Nombre,
        Empresa = a.Obra?.Empresa?.Nombre,
        CodigoAutorizante = r.CodigoRedet,
        AutorizanteEstado = MapeoEstadoRedet.MapearEstadoDesdeId(r.EtapaId).Nombre,
        ExpedientePago = expediente,
        TipoPagoId = ReglasRedeterminaciones.TipoPagoReliquidacionId,
        TipoPago = TipoPagoReliquidacionNombre,
        MontoTotal = reliq.Monto,
        MesAprobacion = reliq.Certificado.MesAprobacion,
        LineaGestion = a.Obra?.Proyecto?.LineaGestion?.Nombre,
        Porcentaje = reliq.PorcentajeAplicado,
    };
}
