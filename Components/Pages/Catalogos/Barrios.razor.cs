#nullable enable
using IVC.Application.Catalogos.Dtos;

namespace IVC.Components.Pages.Catalogos;

/// <summary>Catálogo de barrios (ex AbmlBarrio.aspx + ModificarBarrio.aspx), en una sola grilla con edición inline.</summary>
public partial class Barrios
{
    protected override string PageUrl => "/barrios";
    protected override string TituloEntidad => "barrios";
    protected override string ExportNombreHoja => "Barrios";
    protected override string ExportNombreArchivo => "Barrios.xlsx";

    protected override string DescripcionFila(CatalogoSimpleViewModel item) => $"El barrio \"{item.Nombre}\"";

    protected override string TextoConfirmacionEliminar(CatalogoSimpleViewModel item) =>
        $"Se eliminará el barrio \"{item.Nombre}\". Si alguna obra lo usa, el borrado va a fallar. " +
        "Esta acción no se puede deshacer.";
}
