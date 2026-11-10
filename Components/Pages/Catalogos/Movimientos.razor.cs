#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;

namespace IVC.Components.Pages.Catalogos;

/// <summary>
/// Movimientos de gestión (ex MovimientosGestion.aspx): ajustes de monto por obra. La
/// columna "Autorizado nuevo" depende de todos los movimientos de la obra, así que
/// después de guardar se recarga la grilla (la fila sola no alcanza) y al eliminar se
/// corrige en el lugar la de las filas hermanas.
/// </summary>
public partial class Movimientos
{
    [Inject] private IMovimientosService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _obras = Array.Empty<OpcionLookup>();

    protected override string PageUrl => "/movimientos";
    protected override string TituloEntidad => "movimientos de gestión";
    protected override string ExportNombreHoja => "Movimientos";
    protected override string ExportNombreArchivo => "MovimientosGestion.xlsx";

    protected override async Task CargarAuxiliaresAsync() =>
        _obras = await Lookup.GetObrasAsync();

    protected override async Task<List<MovimientoViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(MovimientoViewModel item) =>
        [item.Obra, item.Proyecto, item.SubProyecto, item.Linea];

    protected override MovimientoViewModel NuevaFila() => new() { Fecha = DateTime.Today };

    protected override void PrepararParaGuardar(MovimientoViewModel item)
    {
        if (item.Id == 0)
            item.Obra = _obras.FirstOrDefault(o => o.Id == item.ObraId)?.Nombre;
    }

    protected override string DescripcionFila(MovimientoViewModel item) =>
        $"El movimiento de {item.Monto:N2} del {item.Fecha:dd/MM/yyyy} en {item.Obra}";

    protected override string TextoConfirmacionEliminar(MovimientoViewModel item) =>
        $"Se eliminará el movimiento de {item.Monto:N2} del {item.Fecha:dd/MM/yyyy} de la obra {item.Obra}. " +
        "Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(MovimientoViewModel item, bool esAlta) =>
        MovimientoValidator.Validar(item, esAlta);

    protected override int? IdAuditoria(MovimientoViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(MovimientoViewModel item) => item.Obra ?? string.Empty;

    protected override async Task CrearAsync(MovimientoViewModel item)
    {
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(MovimientoViewModel item) => Servicio.UpdateAsync(item);

    protected override async Task EliminarAsync(MovimientoViewModel item)
    {
        if (item.Id == 0) return;
        await Servicio.DeleteAsync(item.Id);
        // La baja se refleja en el lugar: el autorizado nuevo de las otras filas de la
        // misma obra baja en el monto eliminado.
        if (item.Monto is decimal monto)
            foreach (var hermana in _items.Where(x => x.ObraId == item.ObraId && !ReferenceEquals(x, item)))
                if (hermana.Autorizado2026 is decimal autorizado)
                    hermana.Autorizado2026 = autorizado - monto;
    }
}
