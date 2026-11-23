#nullable enable

namespace IVC.Services.Abstractions;

public interface IExportService
{
    /// <param name="titulos">
    /// Títulos que reemplazan al [Display] de la propiedad indicada (clave: nombre de la
    /// propiedad). Para columnas cuyo título depende de datos, como los años del ciclo de
    /// Formulación. Null: sólo los atributos.
    /// </param>
    byte[] ExportToXlsx<T>(IReadOnlyList<T> data, string sheetName, IReadOnlyDictionary<string, string>? titulos = null);
}
