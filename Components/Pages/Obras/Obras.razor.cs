#nullable enable
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Obras;
using IVC.Application.Obras.Dtos;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace IVC.Components.Pages.Obras;

/// <summary>
/// Obras (ex ObrasEF.aspx): la grilla con los indicadores por obra, filtrada por las áreas
/// del usuario (admin y Redeterminaciones ven todo), con las columnas financieras ocultas
/// al área Redeterminaciones y alta, edición y baja sólo con la planificación abierta. La
/// edición es en ventana, como el modal del legado; el servicio vuelve a verificar cada
/// regla antes de escribir.
/// </summary>
public partial class Obras
{
    [Inject] private IObrasService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;
    [Inject] private IUsuarioActual UsuarioActual { get; set; } = null!;
    [Inject] private IPermisosIvc Permisos { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _areasPermitidas = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _empresas = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _contratas = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _barrios = Array.Empty<OpcionLookup>();
    private bool _veFinanzas = true;
    private bool _puedeModificar;

    protected override string PageUrl => "/obras";
    protected override string TituloEntidad => "obras";
    protected override string ExportNombreHoja => "Obras";
    protected override string ExportNombreArchivo => "Obras.xlsx";

    protected override async Task CargarAuxiliaresAsync()
    {
        var usuario = await UsuarioActual.ObtenerAsync();
        _veFinanzas = ReglasPermisosIvc.VeColumnasFinancieras(usuario);
        _puedeModificar = await Permisos.PuedeModificarPlanificacionAsync();
        _areasPermitidas = await Servicio.GetAreasPermitidasAsync();
        _empresas = await Lookup.GetEmpresasAsync();
        _contratas = await Lookup.GetContratasAsync();
        _barrios = await Lookup.GetBarriosAsync();
    }

    protected override async Task<List<ObraViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    // Los mismos campos que recorría el filtro de texto del legado.
    protected override IEnumerable<string?> CamposBusqueda(ObraViewModel item) =>
        [item.Descripcion, item.Area, item.Empresa, item.Contrata, item.Barrio, item.LineaGestionNombre,
         item.ProyectoNombre, item.Numero?.ToString(), item.Anio?.ToString()];

    protected override async Task<ObraViewModel?> ObtenerFilaAsync(ObraViewModel item, CancellationToken ct) =>
        item.Id != 0 ? await Servicio.GetByIdAsync(item.Id, ct) : null;

    // Con una sola área permitida, el alta ya viene con ella (el legado la fijaba y bloqueaba).
    protected override ObraViewModel NuevaFila() => new()
    {
        AreaId = _areasPermitidas.Count == 1 ? _areasPermitidas[0].Id : 0,
    };

    // Los desplegables editan Ids ([AuditIgnore]): se resuelven los nombres para que el
    // historial muestre "Empresa: A → B" y no quede vacío.
    protected override void PrepararParaGuardar(ObraViewModel item)
    {
        item.Descripcion = item.Descripcion.Trim();
        item.Area = _areasPermitidas.FirstOrDefault(a => a.Id == item.AreaId)?.Nombre ?? item.Area;
        item.Empresa = _empresas.FirstOrDefault(e => e.Id == item.EmpresaId)?.Nombre;
        item.Contrata = _contratas.FirstOrDefault(c => c.Id == item.ContrataId)?.Nombre;
        item.Barrio = _barrios.FirstOrDefault(b => b.Id == item.BarrioId)?.Nombre;
    }

    protected override string DescripcionFila(ObraViewModel item) => $"El registro de obra \"{item.Descripcion}\"";

    protected override string TextoConfirmacionEliminar(ObraViewModel item) =>
        $"Se eliminará la obra \"{item.Descripcion}\" ({item.Empresa}, {item.Area}). Si tiene autorizantes, legítimos, " +
        "formulación, proyecto o movimientos asociados, el borrado va a fallar. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(ObraViewModel item, bool esAlta) => ObraValidator.Validar(item);

    protected override int? IdAuditoria(ObraViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(ObraViewModel item) => item.Descripcion;

    protected override async Task CrearAsync(ObraViewModel item)
    {
        // Vuelve releída con los nombres de catálogo y los indicadores en cero.
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(ObraViewModel item) => Servicio.UpdateAsync(item);

    protected override Task EliminarAsync(ObraViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;

    /// <summary>Alta en ventana: el panel de identidad se completa en vivo con lo elegido.</summary>
    private async Task NuevoEnVentana()
    {
        var item = NuevaFila();
        await DialogService.OpenAsync<ObraEditor>(
            "Nueva obra",
            new Dictionary<string, object?>
            {
                [nameof(ObraEditor.Item)] = item,
                [nameof(ObraEditor.Borrador)] = item,
                [nameof(ObraEditor.EsAlta)] = true,
                [nameof(ObraEditor.AreaEditable)] = _areasPermitidas.Count != 1,
                [nameof(ObraEditor.Areas)] = _areasPermitidas,
                [nameof(ObraEditor.Empresas)] = _empresas,
                [nameof(ObraEditor.Contratas)] = _contratas,
                [nameof(ObraEditor.Barrios)] = _barrios,
                [nameof(ObraEditor.GuardarAsync)] = (Func<Task<bool>>)(() => CrearDesdeDialogoAsync(item)),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
    }

    /// <summary>Edición en ventana sobre el borrador; cerrar sin guardar lo descarta. El área queda fija.</summary>
    private async Task EditarEnVentana(ObraViewModel item)
    {
        var resultado = await DialogService.OpenAsync<ObraEditor>(
            $"Editar — {item.Descripcion}",
            new Dictionary<string, object?>
            {
                [nameof(ObraEditor.Item)] = item,
                [nameof(ObraEditor.Borrador)] = Buffer(item),
                [nameof(ObraEditor.EsAlta)] = false,
                [nameof(ObraEditor.AreaEditable)] = false,
                // El área guardada puede no estar entre las permitidas del editor (otro usuario la creó):
                // se agrega para que el desplegable la muestre.
                [nameof(ObraEditor.Areas)] = _areasPermitidas.Any(a => a.Id == item.AreaId)
                    ? _areasPermitidas
                    : _areasPermitidas.Append(new OpcionLookup(item.AreaId, item.Area ?? string.Empty)).ToList(),
                [nameof(ObraEditor.Empresas)] = _empresas,
                [nameof(ObraEditor.Contratas)] = _contratas,
                [nameof(ObraEditor.Barrios)] = _barrios,
                [nameof(ObraEditor.GuardarAsync)] = (Func<Task<bool>>)(() => GuardarDesdeDialogoAsync(item)),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
        if (resultado is not true) DescartarBuffer(item);
    }

    /// <summary>
    /// Export con las mismas columnas que ve el usuario: quien no ve las financieras en la
    /// grilla tampoco las recibe en la planilla.
    /// </summary>
    private async Task ExportarObras()
    {
        if (_veFinanzas)
        {
            await ExportarExcel();
            return;
        }
        if (!CanAccess)
        {
            Notification.ShowError($"No tenés permiso para exportar {TituloEntidad}.", "Permiso denegado");
            return;
        }
        _exportando = true;
        try
        {
            if (_grid is not null) await _grid.Reload();
            var filas = (_grid?.View?.ToList() ?? _items).Select(ObraExportSinFinanzas.De).ToList();
            await DescargarXlsxAsync(filas, ExportNombreHoja, ExportNombreArchivo);
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
}
