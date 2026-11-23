#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

/// <summary>
/// Filas FORMULACION del ciclo vigente (ex FormulacionNegocioEF). Todas las operaciones
/// reciben los años del ciclo: lo que queda fuera de la ventana no se lee ni se modifica.
/// </summary>
public interface IFormulacionRepository
{
    /// <summary>
    /// Filas del ciclo con obra y catálogos, de las áreas indicadas (null = todas), ordenadas
    /// por descripción de obra. El servicio las agrupa por obra para armar el pivot.
    /// </summary>
    Task<IReadOnlyList<Formulacion>> GetDelCicloAsync(IReadOnlyCollection<int> anios, IReadOnlyCollection<int>? areaIds, CancellationToken ct = default);

    /// <summary>Filas del ciclo de una obra, con obra y catálogos. Vacío si no tiene formulación en el ciclo.</summary>
    Task<IReadOnlyList<Formulacion>> GetDeObraAsync(int obraId, IReadOnlyCollection<int> anios, CancellationToken ct = default);

    /// <summary>
    /// Guarda la formulación de una obra para el ciclo (alta y edición unificadas). Por cada
    /// año en orden: con monto, inserta o actualiza la fila con los campos compartidos de
    /// <paramref name="compartido"/>; sin monto, elimina la fila de ese año si existía. Todo
    /// en un solo SaveChanges.
    /// </summary>
    Task GuardarPivotAsync(int obraId, IReadOnlyList<int> anios, Formulacion compartido, IReadOnlyList<decimal?> montos, CancellationToken ct = default);

    /// <summary>Elimina las filas del ciclo de la obra. false si no había ninguna.</summary>
    Task<bool> EliminarDeObraAsync(int obraId, IReadOnlyCollection<int> anios, CancellationToken ct = default);
}
