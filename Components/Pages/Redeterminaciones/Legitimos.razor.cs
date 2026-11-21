#nullable enable
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace IVC.Components.Pages.Redeterminaciones;

/// <summary>
/// Legítimos abonos (ex LegitimosEF.aspx). Expediente y monto certificado se editan inline;
/// alta, edición completa y baja van en ventana y sólo con la planificación abierta. El
/// administrador ve todo; el resto, las obras de sus áreas.
/// </summary>
public partial class Legitimos
{
    [Inject] private ILegitimosService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;
    [Inject] private IUsuarioActual UsuarioActual { get; set; } = null!;
    [Inject] private IPermisosIvc Permisos { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _obras = Array.Empty<OpcionLookup>();
    private bool _puedeModificar;
    private bool _puedeInline;
    private bool _enVentana;

    protected override string PageUrl => "/legitimos";
    protected override string TituloEntidad => "legítimos";
    protected override string ExportNombreHoja => "Legitimos";
    protected override string ExportNombreArchivo => "Legitimos.xlsx";

    protected override async Task CargarAuxiliaresAsync()
    {
        var usuario = await UsuarioActual.ObtenerAsync();
        _puedeModificar = await Permisos.PuedeModificarPlanificacionAsync();
        _puedeInline = await Permisos.PuedeEditarInlineAsync();
        _obras = await Lookup.GetObrasAsync(usuario.EsAdmin ? null : ReglasPermisosIvc.AreaIdsParaFiltro(usuario));
    }

    protected override async Task<List<LegitimoViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(LegitimoViewModel item) =>
        [item.Obra, item.Area, item.Empresa, item.Barrio, item.LineaGestion, item.CodigoAutorizante, item.Expediente, item.Estado];

    protected override async Task<LegitimoViewModel?> ObtenerFilaAsync(LegitimoViewModel item, CancellationToken ct) =>
        item.Id != 0 ? await Servicio.GetByIdAsync(item.Id, ct) : null;

    protected override LegitimoViewModel NuevaFila() => new()
    {
        ObraId = _obras.Count == 1 ? _obras[0].Id : 0,
        MesAprobacion = ReglasRedeterminaciones.PrimerDiaDelMes(DateTime.Today),
    };

    protected override void PrepararParaGuardar(LegitimoViewModel item)
    {
        item.CodigoAutorizante = item.CodigoAutorizante?.Trim();
        item.Expediente = item.Expediente?.Trim();
        if (item.Id == 0)
            item.Obra = _obras.FirstOrDefault(o => o.Id == item.ObraId)?.Nombre ?? item.Obra;
    }

    protected override string DescripcionFila(LegitimoViewModel item) =>
        $"El legítimo abono {item.CodigoAutorizante} de {item.Obra}";

    protected override string TextoConfirmacionEliminar(LegitimoViewModel item) =>
        $"Se eliminará el legítimo abono {item.CodigoAutorizante} ({item.Expediente}) de la obra {item.Obra}. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(LegitimoViewModel item, bool esAlta) =>
        esAlta || _enVentana ? LegitimoValidator.Validar(item) : LegitimoValidator.ValidarInline(item);

    protected override int? IdAuditoria(LegitimoViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(LegitimoViewModel item) => item.CodigoAutorizante ?? item.Obra ?? string.Empty;

    protected override async Task CrearAsync(LegitimoViewModel item)
    {
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(LegitimoViewModel item) =>
        _enVentana ? Servicio.UpdateAsync(item) : Servicio.ActualizarInlineAsync(item);

    protected override Task EliminarAsync(LegitimoViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;

    private async Task NuevoEnVentana()
    {
        var item = NuevaFila();
        await DialogService.OpenAsync<LegitimoEditor>(
            "Nuevo legítimo abono",
            new Dictionary<string, object?>
            {
                [nameof(LegitimoEditor.Item)] = item,
                [nameof(LegitimoEditor.Borrador)] = item,
                [nameof(LegitimoEditor.EsAlta)] = true,
                [nameof(LegitimoEditor.Obras)] = _obras,
                [nameof(LegitimoEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => CrearDesdeDialogoAsync(item))),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
    }

    private async Task EditarEnVentana(LegitimoViewModel item)
    {
        var resultado = await DialogService.OpenAsync<LegitimoEditor>(
            $"Editar — {item.CodigoAutorizante}",
            new Dictionary<string, object?>
            {
                [nameof(LegitimoEditor.Item)] = item,
                [nameof(LegitimoEditor.Borrador)] = Buffer(item),
                [nameof(LegitimoEditor.EsAlta)] = false,
                [nameof(LegitimoEditor.Obras)] = _obras,
                [nameof(LegitimoEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => GuardarDesdeDialogoAsync(item))),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
        if (resultado is not true) DescartarBuffer(item);
    }

    private async Task<bool> EnVentanaAsync(Func<Task<bool>> guardar)
    {
        _enVentana = true;
        try
        {
            return await guardar();
        }
        finally
        {
            _enVentana = false;
        }
    }
}
