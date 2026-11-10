#nullable enable
using IVC.Application.Common;

namespace IVC.Repositories.Abstractions;

/// <summary>Opciones de los desplegables de las páginas (obras y líneas de gestión).</summary>
public interface ILookupRepository
{
    /// <summary>Todas las obras, con el texto del legado: barrio - descripción - contrata - número/año.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(CancellationToken ct = default);
    /// <summary>Obras que todavía no tienen fila en BD_PROYECTOS (alta de proyecto); el texto agrega la empresa.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetObrasSinProyectoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetLineasGestionAsync(CancellationToken ct = default);
}
