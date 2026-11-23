#nullable enable
using IVC.Application.Common;
using IVC.Application.Formulacion;
using IVC.Application.Formulacion.Dtos;

namespace IVC.Services.Abstractions;

/// <summary>
/// Formulación presupuestaria por obra para el ciclo vigente. Aplica las reglas del
/// usuario actual: qué obras ve (todas el administrador, las de sus áreas el resto), si
/// puede modificar (formulación abierta; nunca Redeterminaciones ni Secretaría) y si ve
/// el PPI (sólo el administrador).
/// </summary>
public interface IFormulacionService
{
    /// <summary>Ciclo vigente (año base de la configuración y sus dos años siguientes).</summary>
    FormulacionCiclo Ciclo { get; }

    /// <summary>Una fila por obra con formulación en el ciclo, ordenadas por obra.</summary>
    Task<IReadOnlyList<FormulacionViewModel>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Pivot de una obra, o null si no tiene formulación en el ciclo.</summary>
    Task<FormulacionViewModel?> GetPorObraAsync(int obraId, CancellationToken ct = default);

    /// <summary>
    /// Alta o edición: por cada año del ciclo inserta, actualiza o elimina la fila según
    /// venga o no el monto. Devuelve la fila releída.
    /// </summary>
    Task<FormulacionViewModel> GuardarAsync(FormulacionViewModel vm, bool esAlta, CancellationToken ct = default);

    /// <summary>Elimina toda la formulación del ciclo de la obra.</summary>
    Task EliminarAsync(int obraId, CancellationToken ct = default);

    /// <summary>Obras del usuario que todavía no tienen formulación en el ciclo (para el alta).</summary>
    Task<IReadOnlyList<OpcionLookup>> GetObrasDisponiblesAsync(CancellationToken ct = default);
}
