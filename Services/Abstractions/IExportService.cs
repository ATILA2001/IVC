#nullable enable

namespace IVC.Services.Abstractions;

public interface IExportService
{
    byte[] ExportToXlsx<T>(IReadOnlyList<T> data, string sheetName);
}
