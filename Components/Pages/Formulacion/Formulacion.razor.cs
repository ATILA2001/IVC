#nullable enable
using IVC.Application.Common;
using IVC.Application.Formulacion;
using IVC.Application.Formulacion.Dtos;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace IVC.Components.Pages.Formulacion;

/// <summary>
/// Formulación presupuestaria (ex FormulacionEF.aspx): una fila por obra con los montos de
/// los tres años del ciclo vigente. Alta, edición y baja en ventana, sólo con la formulación
/// abierta; sin edición inline, como el legado. El PPI lo ve y edita sólo el administrador,
/// que es también el único que edita el techo (el panel oculto del legado); el export y el
/// historial del resto salen sin PPI, como su grilla.
/// </summary>
public partial class Formulacion
{
    [Inject] private IFormulacionService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;
    [Inject] private IUsuarioActual UsuarioActual { get; set; } = null!;
    [Inject] private IPermisosIvc Permisos { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _unidades = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _prioridades = Array.Empty<OpcionLookup>();
    private bool _puedeModificar;
    private bool _esAdmin;

    // Campos del historial que el usuario no administrador no ve en pantalla.
    private static readonly string[] CamposSoloAdmin = ["PPI"];

    private int[] Anios => Servicio.Ciclo.Anios;

    protected override string PageUrl => "/formulacion";
    protected override string TituloEntidad => "formulaciones";
    protected override string ExportNombreHoja => "Formulacion";
    protected override string ExportNombreArchivo => "Formulacion.xlsx";

    // Las columnas de monto llevan el año del ciclo, en pantalla y en la planilla.
    protected override IReadOnlyDictionary<string, string>? TitulosExport => new Dictionary<string, string>
    {
        [nameof(FormulacionViewModel.MontoAnio1)] = $"Monto {Anios[0]}",
        [nameof(FormulacionViewModel.MontoAnio2)] = $"Monto {Anios[1]}",
        [nameof(FormulacionViewModel.MontoAnio3)] = $"Monto {Anios[2]}",
    };

    protected override async Task CargarAuxiliaresAsync()
    {
        var usuario = await UsuarioActual.ObtenerAsync();
        _esAdmin = usuario.EsAdmin;
        _puedeModificar = await Permisos.PuedeModificarFormulacionAsync();
        _unidades = await Lookup.GetUnidadesMedidaAsync();
        _prioridades = await Lookup.GetPrioridadesAsync();
    }

    protected override async Task<List<FormulacionViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    // Los mismos campos que recorría el filtro de texto del legado.
    protected override IEnumerable<string?> CamposBusqueda(FormulacionViewModel item) =>
        [item.Obra, item.Empresa, item.Contrata, item.Barrio, item.Observaciones, item.Prioridad, item.Proyecto, item.LineaGestion];

    protected override async Task<FormulacionViewModel?> ObtenerFilaAsync(FormulacionViewModel item, CancellationToken ct) =>
        await Servicio.GetPorObraAsync(item.ObraId, ct);

    // Los desplegables editan Ids ([AuditIgnore]): se resuelven los nombres para el historial.
    protected override void PrepararParaGuardar(FormulacionViewModel item)
    {
        item.BreveDescripcion = item.BreveDescripcion?.Trim();
        item.Observaciones = item.Observaciones?.Trim();
        item.UnidadMedida = _unidades.FirstOrDefault(u => u.Id == item.UnidadMedidaId)?.Nombre ?? item.UnidadMedida;
        item.Prioridad = _prioridades.FirstOrDefault(p => p.Id == item.PrioridadId)?.Nombre ?? item.Prioridad;
    }

    protected override string DescripcionFila(FormulacionViewModel item) => $"La formulación de la obra \"{item.Obra}\"";

    protected override string TextoConfirmacionEliminar(FormulacionViewModel item) =>
        $"Se eliminará la formulación de la obra \"{item.Obra}\" para los años {Anios[0]}, {Anios[1]} y {Anios[2]}. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(FormulacionViewModel item, bool esAlta) =>
        FormulacionValidator.Validar(item, esAlta, editaPpiYTecho: _esAdmin);

    // La fila no tiene Id propio: el historial se busca por obra.
    protected override int? IdAuditoria(FormulacionViewModel item) => item.ObraId;
    protected override string ClaveAuditoria(FormulacionViewModel item) => item.Obra ?? item.ObraId.ToString();

    protected override async Task CrearAsync(FormulacionViewModel item)
    {
        var creado = await Servicio.GuardarAsync(item, esAlta: true);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(FormulacionViewModel item) => Servicio.GuardarAsync(item, esAlta: false);

    protected override Task EliminarAsync(FormulacionViewModel item) => Servicio.EliminarAsync(item.ObraId);

    private async Task NuevoEnVentana()
    {
        var item = NuevaFila();
        // Sólo obras del usuario sin formulación en el ciclo, releídas en cada alta.
        var obras = await Servicio.GetObrasDisponiblesAsync();
        if (obras.Count == 0)
        {
            Notification.ShowWarning("Todas tus obras ya tienen formulación en el ciclo vigente.", "Sin obras disponibles");
            return;
        }
        await DialogService.OpenAsync<FormulacionEditor>(
            "Nueva formulación",
            new Dictionary<string, object?>
            {
                [nameof(FormulacionEditor.Item)] = item,
                [nameof(FormulacionEditor.Borrador)] = item,
                [nameof(FormulacionEditor.EsAlta)] = true,
                [nameof(FormulacionEditor.Anios)] = Anios,
                [nameof(FormulacionEditor.EsAdmin)] = _esAdmin,
                [nameof(FormulacionEditor.Obras)] = obras,
                [nameof(FormulacionEditor.Unidades)] = _unidades,
                [nameof(FormulacionEditor.Prioridades)] = _prioridades,
                [nameof(FormulacionEditor.GuardarAsync)] = (Func<Task<bool>>)(() => CrearDesdeDialogoAsync(item)),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
    }

    private async Task EditarEnVentana(FormulacionViewModel item)
    {
        var resultado = await DialogService.OpenAsync<FormulacionEditor>(
            $"Editar — {item.Obra}",
            new Dictionary<string, object?>
            {
                [nameof(FormulacionEditor.Item)] = item,
                [nameof(FormulacionEditor.Borrador)] = Buffer(item),
                [nameof(FormulacionEditor.EsAlta)] = false,
                [nameof(FormulacionEditor.Anios)] = Anios,
                [nameof(FormulacionEditor.EsAdmin)] = _esAdmin,
                [nameof(FormulacionEditor.Obras)] = Array.Empty<OpcionLookup>(),
                [nameof(FormulacionEditor.Unidades)] = _unidades,
                [nameof(FormulacionEditor.Prioridades)] = _prioridades,
                [nameof(FormulacionEditor.GuardarAsync)] = (Func<Task<bool>>)(() => GuardarDesdeDialogoAsync(item)),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
        if (resultado is not true) DescartarBuffer(item);
    }

    /// <summary>
    /// Export con las columnas que el usuario ve: el administrador exporta la fila completa;
    /// el resto, la fila sin PPI (misma receta que Obras para las columnas financieras).
    /// </summary>
    private async Task ExportarFormulacion()
    {
        if (_esAdmin)
        {
            await ExportarExcel();
            return;
        }
        if (!CanAccess)
        {
            Notification.ShowError($"No tenés permiso para exportar {TituloEntidad}.", "Permiso denegado");
            return;
        }
        if (_cargandoResto || _cargaIncompleta)
        {
            Notification.ShowWarning("Esperá a que termine de cargar la grilla para exportar.", "Carga en curso");
            return;
        }
        _exportando = true;
        try
        {
            if (_grid is not null) await _grid.Reload();
            var filas = (_grid?.View?.ToList() ?? _items).Select(FormulacionExportSinPpi.De).ToList();
            await DescargarXlsxAsync(filas, ExportNombreHoja, ExportNombreArchivo, TitulosExport);
        }
        catch (Exception ex)
        {
            Informar(ex, "El export", "Error al exportar");
        }
        finally
        {
            _exportando = false;
        }
    }

    /// <summary>Historial de la fila; para el no administrador, sin los cambios del PPI.</summary>
    private async Task VerHistorialFormulacion(FormulacionViewModel item)
    {
        if (!CanAccess)
        {
            Notification.ShowError($"No tenés permiso para ver {TituloEntidad}.", "Permiso denegado");
            return;
        }
        await DialogService.OpenAsync<Shared.HistorialAuditoria>(
            $"Historial — {DescripcionFila(item)}",
            new Dictionary<string, object?>
            {
                [nameof(Shared.HistorialAuditoria.Vista)] = PageUrl,
                [nameof(Shared.HistorialAuditoria.EntidadId)] = IdAuditoria(item),
                [nameof(Shared.HistorialAuditoria.ClaveNegocio)] = ClaveAuditoria(item),
                [nameof(Shared.HistorialAuditoria.CamposOcultos)] = _esAdmin ? null : CamposSoloAdmin,
            },
            new DialogOptions { Width = "720px" });
    }
}
