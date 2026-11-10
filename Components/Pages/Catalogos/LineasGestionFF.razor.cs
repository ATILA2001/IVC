#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;

namespace IVC.Components.Pages.Catalogos;

/// <summary>
/// Fuentes de financiamiento por línea de gestión (ex LineasGestionFF.aspx). El legado
/// sólo permitía agregar; acá también se edita y se elimina.
/// </summary>
public partial class LineasGestionFF
{
    [Inject] private ILineasGestionFFService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _lineas = Array.Empty<OpcionLookup>();

    protected override string PageUrl => "/lineas-gestion-ff";
    protected override string TituloEntidad => "líneas de gestión FF";
    protected override string ExportNombreHoja => "Líneas de gestión FF";
    protected override string ExportNombreArchivo => "LineasGestionFF.xlsx";

    protected override async Task CargarAuxiliaresAsync() =>
        _lineas = await Lookup.GetLineasGestionAsync();

    protected override async Task<List<LineaGestionFFViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(LineaGestionFFViewModel item) =>
        [item.LineaGestionNombre, item.Nombre, item.Fuente];

    // Relectura de a una fila: trae tipo, grupo y repartición de la línea recién elegida
    // sin recargar la grilla.
    protected override async Task<LineaGestionFFViewModel?> ObtenerFilaAsync(LineaGestionFFViewModel item, CancellationToken ct) =>
        item.Id != 0 ? await Servicio.GetByIdAsync(item.Id, ct) : null;

    // El desplegable edita LineaGestionId ([AuditIgnore]): sin resolver el nombre visible
    // antes del diff, el cambio de línea no dejaría rastro en el historial.
    protected override void PrepararParaGuardar(LineaGestionFFViewModel item)
    {
        item.Nombre = item.Nombre.Trim();
        item.Fuente = string.IsNullOrWhiteSpace(item.Fuente) ? null : item.Fuente.Trim();
        item.LineaGestionNombre = _lineas.FirstOrDefault(l => l.Id == item.LineaGestionId)?.Nombre;
    }

    protected override string DescripcionFila(LineaGestionFFViewModel item) =>
        $"El registro \"{item.Nombre}\" de {item.LineaGestionNombre}";

    protected override string TextoConfirmacionEliminar(LineaGestionFFViewModel item) =>
        $"Se eliminará la fuente \"{item.Nombre}\" de la línea {item.LineaGestionNombre}. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(LineaGestionFFViewModel item, bool esAlta) =>
        LineaGestionFFValidator.Validar(item);

    protected override int? IdAuditoria(LineaGestionFFViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(LineaGestionFFViewModel item) => $"{item.LineaGestionNombre}:{item.Nombre}";

    protected override async Task CrearAsync(LineaGestionFFViewModel item)
    {
        // La fila vuelve completa (Id y datos de la línea): se pisa la de pantalla.
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
    }

    protected override Task ActualizarAsync(LineaGestionFFViewModel item) => Servicio.UpdateAsync(item);

    protected override Task EliminarAsync(LineaGestionFFViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;
}
