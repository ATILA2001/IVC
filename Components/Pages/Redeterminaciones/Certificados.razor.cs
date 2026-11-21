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
/// Certificados (ex CertificadosEF.aspx): los certificados reales con las reliquidaciones
/// virtuales que generan las redeterminaciones. Inline se editan expediente, monto y
/// ejecución física (en las reliquidaciones sólo el expediente, que va a
/// EXPEDIENTES_RELIQ); alta, edición completa y baja van en ventana y sólo con la
/// planificación abierta. El administrador ve todo; el resto, sus áreas.
/// </summary>
public partial class Certificados
{
    [Inject] private ICertificadosService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;
    [Inject] private IUsuarioActual UsuarioActual { get; set; } = null!;
    [Inject] private IPermisosIvc Permisos { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _autorizantes = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _tiposPago = Array.Empty<OpcionLookup>();
    private bool _puedeModificar;
    private bool _puedeInline;
    private bool _enVentana;

    protected override string PageUrl => "/certificados";
    protected override string TituloEntidad => "certificados";
    protected override string ExportNombreHoja => "Certificados";
    protected override string ExportNombreArchivo => "Certificados.xlsx";

    protected override async Task CargarAuxiliaresAsync()
    {
        var usuario = await UsuarioActual.ObtenerAsync();
        _puedeModificar = await Permisos.PuedeModificarPlanificacionAsync();
        _puedeInline = await Permisos.PuedeEditarInlineAsync();
        _autorizantes = await Lookup.GetAutorizantesAsync(usuario.EsAdmin ? null : ReglasPermisosIvc.AreaIdsParaFiltro(usuario));
        _tiposPago = await Lookup.GetTiposPagoAsync();
    }

    protected override async Task<List<CertificadoViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(CertificadoViewModel item) =>
        [item.Obra, item.Area, item.Empresa, item.Contrata, item.Barrio, item.Proyecto, item.LineaGestion, item.CodigoAutorizante, item.ExpedientePago, item.TipoPago, item.Estado];

    // El SIGAF se prorratea entre los pagos del mismo expediente: una fila sola no alcanza, se recarga todo.
    protected override Task<CertificadoViewModel?> ObtenerFilaAsync(CertificadoViewModel item, CancellationToken ct) =>
        Task.FromResult<CertificadoViewModel?>(null);

    protected override CertificadoViewModel NuevaFila() => new()
    {
        MesAprobacion = ReglasRedeterminaciones.PrimerDiaDelMes(DateTime.Today),
    };

    protected override void PrepararParaGuardar(CertificadoViewModel item)
    {
        item.ExpedientePago = item.ExpedientePago?.Trim();
        item.TipoPago = _tiposPago.FirstOrDefault(t => t.Id == item.TipoPagoId)?.Nombre ?? item.TipoPago;
        if (item.Id == 0 && !item.EsReliquidacion)
            item.CodigoAutorizante = _autorizantes.FirstOrDefault(a => a.Id == item.AutorizanteId)?.Nombre ?? item.CodigoAutorizante;
    }

    protected override string DescripcionFila(CertificadoViewModel item) =>
        item.EsReliquidacion
            ? $"La reliquidación {item.CodigoAutorizante} de {item.MesAprobacion:MM/yyyy}"
            : $"El certificado de {item.CodigoAutorizante} ({item.MesAprobacion:MM/yyyy})";

    protected override string TextoConfirmacionEliminar(CertificadoViewModel item) =>
        $"Se eliminará el certificado de {item.CodigoAutorizante} de {item.MesAprobacion:MM/yyyy} por {item.MontoTotal:N2} ({item.Obra}). Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(CertificadoViewModel item, bool esAlta) =>
        esAlta || _enVentana ? CertificadoValidator.Validar(item, esAlta) : CertificadoValidator.ValidarInline(item);

    // La reliquidación no tiene id: su historial se lleva por clave (código de redeterminación y mes).
    protected override int? IdAuditoria(CertificadoViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(CertificadoViewModel item) =>
        item.EsReliquidacion ? item.ClaveReliquidacion ?? string.Empty : $"{item.CodigoAutorizante} {item.MesAprobacion:MM/yyyy}";

    protected override async Task CrearAsync(CertificadoViewModel item)
    {
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(CertificadoViewModel item) =>
        _enVentana ? Servicio.UpdateAsync(item) : Servicio.ActualizarInlineAsync(item);

    protected override Task EliminarAsync(CertificadoViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;

    private async Task NuevoEnVentana()
    {
        var item = NuevaFila();
        await DialogService.OpenAsync<CertificadoEditor>(
            "Nuevo certificado",
            new Dictionary<string, object?>
            {
                [nameof(CertificadoEditor.Item)] = item,
                [nameof(CertificadoEditor.Borrador)] = item,
                [nameof(CertificadoEditor.EsAlta)] = true,
                [nameof(CertificadoEditor.Autorizantes)] = _autorizantes,
                [nameof(CertificadoEditor.TiposPago)] = _tiposPago,
                [nameof(CertificadoEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => CrearDesdeDialogoAsync(item))),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
    }

    private async Task EditarEnVentana(CertificadoViewModel item)
    {
        var resultado = await DialogService.OpenAsync<CertificadoEditor>(
            $"Editar — {item.CodigoAutorizante}",
            new Dictionary<string, object?>
            {
                [nameof(CertificadoEditor.Item)] = item,
                [nameof(CertificadoEditor.Borrador)] = Buffer(item),
                [nameof(CertificadoEditor.EsAlta)] = false,
                [nameof(CertificadoEditor.Autorizantes)] = _autorizantes,
                [nameof(CertificadoEditor.TiposPago)] = _tiposPago,
                [nameof(CertificadoEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => GuardarDesdeDialogoAsync(item))),
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
