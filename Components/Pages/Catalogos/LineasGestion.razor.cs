#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;

namespace IVC.Components.Pages.Catalogos;

/// <summary>
/// Catálogo de líneas de gestión (ex LineasGestion.aspx). El legado sólo permitía
/// agregar; acá también se edita y se elimina, con el borrado rechazado si la línea
/// está en uso.
/// </summary>
public partial class LineasGestion
{
    [Inject] private ILineasGestionService Servicio { get; set; } = null!;

    protected override string PageUrl => "/lineas-gestion";
    protected override string TituloEntidad => "líneas de gestión";
    protected override string ExportNombreHoja => "Líneas de gestión";
    protected override string ExportNombreArchivo => "LineasGestion.xlsx";

    protected override async Task<List<LineaGestionViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(LineaGestionViewModel item) =>
        [item.Nombre, item.Tipo, item.Grupo, item.Reparticion];

    protected override void PrepararParaGuardar(LineaGestionViewModel item)
    {
        item.Nombre = item.Nombre.Trim();
        item.Tipo = Limpio(item.Tipo);
        item.Grupo = Limpio(item.Grupo);
        item.Reparticion = Limpio(item.Reparticion);
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    protected override string DescripcionFila(LineaGestionViewModel item) => $"El registro \"{item.Nombre}\"";

    protected override string TextoConfirmacionEliminar(LineaGestionViewModel item) =>
        $"Se eliminará la línea de gestión \"{item.Nombre}\". Si algún proyecto o fuente de financiamiento la usa, " +
        "el borrado va a fallar. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(LineaGestionViewModel item, bool esAlta)
    {
        var errores = LineaGestionValidator.Validar(item).ToList();
        if (_items.Any(x => x.Id != item.Id
                && string.Equals(x.Nombre.Trim(), item.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
            errores.Add($"Ya existe una línea de gestión \"{item.Nombre.Trim()}\".");
        return errores;
    }

    protected override int? IdAuditoria(LineaGestionViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(LineaGestionViewModel item) => item.Nombre;

    protected override async Task CrearAsync(LineaGestionViewModel item)
    {
        var creado = await Servicio.CreateAsync(item);
        item.Id = creado.Id;
    }

    protected override Task ActualizarAsync(LineaGestionViewModel item) => Servicio.UpdateAsync(item);

    protected override Task EliminarAsync(LineaGestionViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;
}
