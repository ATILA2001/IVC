#nullable enable
using IVC.Application.Common;

namespace IVC.Repositories.Abstractions;

/// <summary>Opciones de los desplegables de las páginas (obras y líneas de gestión).</summary>
public interface ILookupRepository
{
    /// <summary>Todas las obras, con el texto del legado: barrio - descripción - contrata - número/año.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(CancellationToken ct = default);
    /// <summary>Obras de las áreas indicadas (todas si es null; ninguna si es vacío), con el texto del desplegable del legado.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetObrasAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default);
    /// <summary>Obras que todavía no tienen fila en BD_PROYECTOS (alta de proyecto); el texto agrega la empresa.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetObrasSinProyectoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetLineasGestionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetAreasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetEmpresasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetContratasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetBarriosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetConceptosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetEstadosAutorizanteAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetEstadosRedetAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OpcionLookup>> GetTiposPagoAsync(CancellationToken ct = default);
    /// <summary>Usuarios activos del área Redeterminaciones (tabla USUARIOS del legado), para asignar redeterminaciones.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetUsuariosRedeterminacionesAsync(CancellationToken ct = default);
    /// <summary>Autorizantes para los desplegables de alta: "código - obra - empresa - detalle". Filtrados por área si se indica.</summary>
    Task<IReadOnlyList<OpcionLookup>> GetAutorizantesAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default);
}
