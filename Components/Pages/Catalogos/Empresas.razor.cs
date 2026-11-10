#nullable enable
using IVC.Application.Catalogos.Dtos;

namespace IVC.Components.Pages.Catalogos;

/// <summary>Catálogo de empresas contratistas (ex AbmlEmpresa.aspx + ModificarEmpresa.aspx).</summary>
public partial class Empresas
{
    protected override string PageUrl => "/empresas";
    protected override string TituloEntidad => "empresas";
    protected override string ExportNombreHoja => "Empresas";
    protected override string ExportNombreArchivo => "Empresas.xlsx";

    // "El registro" y no "La empresa": los mensajes de la base terminan en "creado" /
    // "eliminado" y el sustantivo neutro evita la concordancia forzada.
    protected override string DescripcionFila(CatalogoSimpleViewModel item) => $"El registro \"{item.Nombre}\"";

    protected override string TextoConfirmacionEliminar(CatalogoSimpleViewModel item) =>
        $"Se eliminará la empresa \"{item.Nombre}\". Si alguna obra la usa, el borrado va a fallar. " +
        "Esta acción no se puede deshacer.";
}
