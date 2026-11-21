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
/// Autorizantes (ex AutorizantesEF.aspx): los autorizantes reales con una fila virtual por
/// redeterminación. Expediente y estado se editan inline en las filas reales (cualquier
/// usuario autenticado, dictamen del 04/11/2026); alta, edición completa y baja van en
/// ventana y sólo con la planificación abierta. Admin y Redeterminaciones ven todas las
/// obras; Redeterminaciones no ve mes base ni SADE. El servicio revalida cada regla.
/// </summary>
public partial class Autorizantes
{
    [Inject] private IAutorizantesService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;
    [Inject] private IUsuarioActual UsuarioActual { get; set; } = null!;
    [Inject] private IPermisosIvc Permisos { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _obras = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _conceptos = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _estados = Array.Empty<OpcionLookup>();
    private bool _puedeModificar;
    private bool _puedeInline;
    private bool _veSeguimiento = true;
    /// <summary>true mientras se guarda desde la ventana: edición completa y no de seguimiento.</summary>
    private bool _enVentana;

    protected override string PageUrl => "/autorizantes";
    protected override string TituloEntidad => "autorizantes";
    protected override string ExportNombreHoja => "Autorizantes";
    protected override string ExportNombreArchivo => "Autorizantes.xlsx";

    protected override async Task CargarAuxiliaresAsync()
    {
        var usuario = await UsuarioActual.ObtenerAsync();
        _veSeguimiento = !ReglasPermisosIvc.EsDeArea(usuario, IvcAreaIds.Redeterminaciones);
        _puedeModificar = await Permisos.PuedeModificarPlanificacionAsync();
        _puedeInline = await Permisos.PuedeEditarInlineAsync();
        // El alta elige entre las obras de las áreas del usuario (el administrador, cualquiera).
        _obras = await Lookup.GetObrasAsync(usuario.EsAdmin ? null : ReglasPermisosIvc.AreaIdsParaFiltro(usuario));
        _conceptos = await Lookup.GetConceptosAsync();
        _estados = await Lookup.GetEstadosAutorizanteAsync();
    }

    protected override async Task<List<AutorizanteViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(AutorizanteViewModel item) =>
        [item.Obra, item.Area, item.Empresa, item.Contrata, item.Barrio, item.CodigoAutorizante, item.Expediente, item.Detalle, item.Concepto, item.Estado];

    protected override async Task<AutorizanteViewModel?> ObtenerFilaAsync(AutorizanteViewModel item, CancellationToken ct) =>
        item.Id != 0 ? await Servicio.GetByIdAsync(item.Id, ct) : null;

    protected override AutorizanteViewModel NuevaFila() => new()
    {
        ObraId = _obras.Count == 1 ? _obras[0].Id : 0,
        MesAprobacion = ReglasRedeterminaciones.PrimerDiaDelMes(DateTime.Today),
    };

    // Los desplegables editan Ids ([AuditIgnore]): se resuelven los nombres para el historial.
    protected override void PrepararParaGuardar(AutorizanteViewModel item)
    {
        item.Detalle = item.Detalle?.Trim();
        item.Expediente = item.Expediente?.Trim();
        item.Concepto = _conceptos.FirstOrDefault(c => c.Id == item.ConceptoId)?.Nombre ?? item.Concepto;
        item.Estado = _estados.FirstOrDefault(e => e.Id == item.EstadoId)?.Nombre ?? item.Estado;
        if (item.Id == 0 && !item.EsRedeterminacion)
            item.Obra = _obras.FirstOrDefault(o => o.Id == item.ObraId)?.Nombre ?? item.Obra;
    }

    protected override string DescripcionFila(AutorizanteViewModel item) =>
        $"El autorizante {item.CodigoAutorizante ?? "nuevo"} de {item.Obra}";

    protected override string TextoConfirmacionEliminar(AutorizanteViewModel item) =>
        $"Se eliminará el autorizante {item.CodigoAutorizante} ({item.Detalle}) de la obra {item.Obra}. " +
        "Si tiene certificados o redeterminaciones, el borrado va a fallar. Esta acción no se puede deshacer.";

    // Inline: sólo expediente y estado. Alta y ventana: la fila completa.
    protected override IReadOnlyList<string> Validar(AutorizanteViewModel item, bool esAlta) =>
        esAlta || _enVentana ? AutorizanteValidator.Validar(item, esAlta) : AutorizanteValidator.ValidarSeguimiento(item);

    protected override int? IdAuditoria(AutorizanteViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(AutorizanteViewModel item) => item.CodigoAutorizante ?? item.Obra ?? string.Empty;

    protected override async Task CrearAsync(AutorizanteViewModel item)
    {
        // Vuelve releído con el código que asignó el trigger y los nombres de catálogo.
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(AutorizanteViewModel item) =>
        _enVentana ? Servicio.UpdateAsync(item) : Servicio.ActualizarSeguimientoAsync(item);

    protected override Task EliminarAsync(AutorizanteViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;

    private static string EtiquetaFila(AutorizanteViewModel item) => item.EsRedeterminacion ? "Redeterminación" : "Autorizante";

    private async Task NuevoEnVentana()
    {
        var item = NuevaFila();
        await DialogService.OpenAsync<AutorizanteEditor>(
            "Nuevo autorizante",
            new Dictionary<string, object?>
            {
                [nameof(AutorizanteEditor.Item)] = item,
                [nameof(AutorizanteEditor.Borrador)] = item,
                [nameof(AutorizanteEditor.EsAlta)] = true,
                [nameof(AutorizanteEditor.Obras)] = _obras,
                [nameof(AutorizanteEditor.Conceptos)] = _conceptos,
                [nameof(AutorizanteEditor.Estados)] = _estados,
                [nameof(AutorizanteEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => CrearDesdeDialogoAsync(item))),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
    }

    private async Task EditarEnVentana(AutorizanteViewModel item)
    {
        var resultado = await DialogService.OpenAsync<AutorizanteEditor>(
            $"Editar — {item.CodigoAutorizante}",
            new Dictionary<string, object?>
            {
                [nameof(AutorizanteEditor.Item)] = item,
                [nameof(AutorizanteEditor.Borrador)] = Buffer(item),
                [nameof(AutorizanteEditor.EsAlta)] = false,
                [nameof(AutorizanteEditor.Obras)] = _obras,
                [nameof(AutorizanteEditor.Conceptos)] = _conceptos,
                [nameof(AutorizanteEditor.Estados)] = _estados,
                [nameof(AutorizanteEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => GuardarDesdeDialogoAsync(item))),
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
