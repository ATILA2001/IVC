#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;

namespace IVC.Components.Pages.Catalogos;

/// <summary>
/// Base de proyectos (ex BdProyectos.aspx): una fila por obra con proyecto, subproyecto,
/// línea de gestión y autorizados. El alta elige la obra entre las que no tienen
/// proyecto; después la obra queda fija.
/// </summary>
public partial class Proyectos
{
    [Inject] private IProyectosService Servicio { get; set; } = null!;
    [Inject] private ILookupService Lookup { get; set; } = null!;

    private IReadOnlyList<OpcionLookup> _obrasSinProyecto = Array.Empty<OpcionLookup>();
    private IReadOnlyList<OpcionLookup> _lineas = Array.Empty<OpcionLookup>();

    protected override string PageUrl => "/proyectos";
    protected override string TituloEntidad => "proyectos";
    protected override string ExportNombreHoja => "Proyectos";
    protected override string ExportNombreArchivo => "Proyectos.xlsx";

    protected override async Task CargarAuxiliaresAsync()
    {
        _obrasSinProyecto = await Lookup.GetObrasSinProyectoAsync();
        _lineas = await Lookup.GetLineasGestionAsync();
    }

    protected override async Task<List<ProyectoViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    // Los mismos campos que recorría el filtro de texto del legado.
    protected override IEnumerable<string?> CamposBusqueda(ProyectoViewModel item) =>
        [item.Nombre, item.SubProyecto, item.Obra, item.Area, item.Contrata, item.LineaGestionNombre];

    protected override async Task<ProyectoViewModel?> ObtenerFilaAsync(ProyectoViewModel item, CancellationToken ct) =>
        item.Id != 0 ? await Servicio.GetByIdAsync(item.Id, ct) : null;

    protected override void PrepararParaGuardar(ProyectoViewModel item)
    {
        item.Nombre = item.Nombre.Trim();
        item.SubProyecto = string.IsNullOrWhiteSpace(item.SubProyecto) ? null : item.SubProyecto.Trim();
        item.LineaGestionNombre = _lineas.FirstOrDefault(l => l.Id == item.LineaGestionId)?.Nombre;
        if (item.Id == 0)
            item.Obra = _obrasSinProyecto.FirstOrDefault(o => o.Id == item.ObraId)?.Nombre;
    }

    protected override string DescripcionFila(ProyectoViewModel item) => $"El proyecto \"{item.Nombre}\" de {item.Obra}";

    protected override string TextoConfirmacionEliminar(ProyectoViewModel item) =>
        $"Se eliminará el proyecto \"{item.Nombre}\" de la obra {item.Obra}. Esta acción no se puede deshacer.";

    protected override IReadOnlyList<string> Validar(ProyectoViewModel item, bool esAlta) =>
        ProyectoValidator.Validar(item, esAlta);

    protected override int? IdAuditoria(ProyectoViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(ProyectoViewModel item) => item.Obra ?? item.Nombre;

    protected override async Task CrearAsync(ProyectoViewModel item)
    {
        // Vuelve releída con área, contrata y empresa de la obra: se pisa la de pantalla.
        var creado = await Servicio.CreateAsync(item);
        Refrescar(creado, item);
        // La obra ya tiene proyecto: sale del desplegable del próximo alta.
        _obrasSinProyecto = await Lookup.GetObrasSinProyectoAsync();
    }

    protected override Task ActualizarAsync(ProyectoViewModel item) => Servicio.UpdateAsync(item);

    protected override async Task EliminarAsync(ProyectoViewModel item)
    {
        if (item.Id == 0) return;
        await Servicio.DeleteAsync(item.Id);
        // La obra vuelve a estar disponible para un alta.
        _obrasSinProyecto = await Lookup.GetObrasSinProyectoAsync();
    }
}
