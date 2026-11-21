#nullable enable
using IVC.Application.Common;
using IVC.Application.Redeterminaciones;
using IVC.Application.Redeterminaciones.Dtos;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace IVC.Components.Pages.Redeterminaciones;

/// <summary>
/// Redeterminaciones (ex RedeterminacionesEF.aspx): cross-area. Etapa, expediente y usuario
/// asignado se editan inline; alta, edición completa y baja van en ventana. Todo con el
/// permiso de redeterminaciones (todos menos Secretaría). Trae los dos filtros especiales
/// del legado: "control de estados" (el buzón SADE no corresponde a la etapa) y "días en
/// buzón" (semáforo verde / naranja / rojo).
/// </summary>
public partial class Redeterminaciones
{
    [Inject] private IRedeterminacionesService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;
    [Inject] private IPermisosIvc Permisos { get; set; } = null!;

    private static readonly OpcionLookup SinUsuario = new(0, "Sin usuario");

    private IReadOnlyList<OpcionLookup> _autorizantes = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _etapas = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _usuarios = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _usuariosConVacio = Array.Empty<OpcionLookup>();
    private bool _puedeModificar;
    private bool _enVentana;
    private bool _soloDiscrepancias;
    private SeguimientoBuzon.Rango _rangoDias = SeguimientoBuzon.Rango.Todos;

    /// <summary>Opción del desplegable de días (Radzen necesita propiedades con nombre, no una tupla).</summary>
    private sealed record OpcionRango(SeguimientoBuzon.Rango Valor, string Texto);

    private static readonly IReadOnlyList<OpcionRango> RangosDias =
    [
        new(SeguimientoBuzon.Rango.Todos, "Días x buzón: todos"),
        new(SeguimientoBuzon.Rango.MenosDeSiete, "Menos de 7 días"),
        new(SeguimientoBuzon.Rango.DeSieteACatorce, "De 7 a 14 días"),
        new(SeguimientoBuzon.Rango.MasDeCatorce, "Más de 14 días"),
    ];

    protected override string PageUrl => "/redeterminaciones";
    protected override string TituloEntidad => "redeterminaciones";
    protected override string ExportNombreHoja => "Redeterminaciones";
    protected override string ExportNombreArchivo => "Redeterminaciones.xlsx";

    /// <summary>Las filas de la grilla: la búsqueda de la base más los dos filtros especiales.</summary>
    private IEnumerable<RedeterminacionViewModel> FilasVisibles =>
        ItemsVisibles.Where(r => (!_soloDiscrepancias || r.Discrepancia) && SeguimientoBuzon.CumpleRango(r.DiasEnBuzon, _rangoDias));

    private bool HayFiltrosEspeciales => _soloDiscrepancias || _rangoDias != SeguimientoBuzon.Rango.Todos;

    protected override async Task CargarAuxiliaresAsync()
    {
        _puedeModificar = await Permisos.PuedeModificarRedeterminacionesAsync();
        _autorizantes = await Lookup.GetAutorizantesAsync(null);
        _etapas = await Lookup.GetEstadosRedetAsync();
        _usuarios = await Lookup.GetUsuariosRedeterminacionesAsync();
        _usuariosConVacio = _usuarios.Prepend(SinUsuario).ToList();
    }

    protected override async Task<List<RedeterminacionViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(RedeterminacionViewModel item) =>
        [item.CodigoRedet, item.CodigoAutorizante, item.Obra, item.Empresa, item.Contrata, item.Area, item.Etapa, item.Expediente, item.Tipo, item.Observaciones, item.Usuario];

    protected override async Task<RedeterminacionViewModel?> ObtenerFilaAsync(RedeterminacionViewModel item, CancellationToken ct) =>
        item.Id != 0 ? await Servicio.GetByIdAsync(item.Id, ct) : null;

    protected override RedeterminacionViewModel NuevaFila() => new()
    {
        Salto = ReglasRedeterminaciones.PrimerDiaDelMes(DateTime.Today),
    };

    protected override void PrepararParaGuardar(RedeterminacionViewModel item)
    {
        item.Expediente = item.Expediente?.Trim();
        item.Tipo = item.Tipo?.Trim();
        item.Observaciones = item.Observaciones?.Trim();
        item.Etapa = _etapas.FirstOrDefault(e => e.Id == item.EtapaId)?.Nombre ?? item.Etapa;
        // "Sin usuario" (0) es null para el servicio.
        if (item.UsuarioId == 0) item.UsuarioId = null;
        item.Usuario = item.UsuarioId is int u ? _usuarios.FirstOrDefault(x => x.Id == u)?.Nombre : null;
        if (item.Id == 0)
            item.CodigoAutorizante = _autorizantes.FirstOrDefault(a => a.Id == item.AutorizanteId)?.Nombre.Split(" - ")[0] ?? item.CodigoAutorizante;
    }

    protected override string DescripcionFila(RedeterminacionViewModel item) =>
        $"La redeterminación {item.CodigoRedet ?? item.CodigoAutorizante}";

    protected override string TextoConfirmacionEliminar(RedeterminacionViewModel item) =>
        $"Se eliminará la redeterminación {item.CodigoRedet} ({item.Tipo}, {item.Etapa}) de la obra {item.Obra}. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(RedeterminacionViewModel item, bool esAlta) =>
        esAlta || _enVentana ? RedeterminacionValidator.Validar(item, esAlta) : RedeterminacionValidator.ValidarSeguimiento(item);

    protected override int? IdAuditoria(RedeterminacionViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(RedeterminacionViewModel item) => item.CodigoRedet ?? item.CodigoAutorizante ?? string.Empty;

    protected override async Task CrearAsync(RedeterminacionViewModel item)
    {
        // Vuelve releída con el código de redeterminación (columna calculada) y los datos de obra.
        var creada = await Servicio.CreateAsync(item);
        Refrescar(creada, item);
    }

    protected override Task ActualizarAsync(RedeterminacionViewModel item) =>
        _enVentana ? Servicio.UpdateAsync(item) : Servicio.ActualizarSeguimientoAsync(item);

    protected override Task EliminarAsync(RedeterminacionViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;

    private static string ColorDias(int dias) => SeguimientoBuzon.RangoDe(dias) switch
    {
        SeguimientoBuzon.Rango.MenosDeSiete => "var(--rz-success)",
        SeguimientoBuzon.Rango.DeSieteACatorce => "var(--rz-warning)",
        _ => "var(--rz-danger)",
    };

    private async Task LimpiarTodo()
    {
        _soloDiscrepancias = false;
        _rangoDias = SeguimientoBuzon.Rango.Todos;
        await LimpiarFiltros();
    }

    private async Task NuevoEnVentana()
    {
        var item = NuevaFila();
        await DialogService.OpenAsync<RedeterminacionEditor>(
            "Nueva redeterminación",
            new Dictionary<string, object?>
            {
                [nameof(RedeterminacionEditor.Item)] = item,
                [nameof(RedeterminacionEditor.Borrador)] = item,
                [nameof(RedeterminacionEditor.EsAlta)] = true,
                [nameof(RedeterminacionEditor.Autorizantes)] = _autorizantes,
                [nameof(RedeterminacionEditor.Etapas)] = _etapas,
                [nameof(RedeterminacionEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => CrearDesdeDialogoAsync(item))),
            },
            new DialogOptions { Width = "1000px", ShowTitle = false, CssClass = "ivc-dialog-panel" });
    }

    private async Task EditarEnVentana(RedeterminacionViewModel item)
    {
        var resultado = await DialogService.OpenAsync<RedeterminacionEditor>(
            $"Editar — {item.CodigoRedet}",
            new Dictionary<string, object?>
            {
                [nameof(RedeterminacionEditor.Item)] = item,
                [nameof(RedeterminacionEditor.Borrador)] = Buffer(item),
                [nameof(RedeterminacionEditor.EsAlta)] = false,
                [nameof(RedeterminacionEditor.Autorizantes)] = _autorizantes,
                [nameof(RedeterminacionEditor.Etapas)] = _etapas,
                [nameof(RedeterminacionEditor.GuardarAsync)] = (Func<Task<bool>>)(() => EnVentanaAsync(() => GuardarDesdeDialogoAsync(item))),
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
