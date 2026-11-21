#nullable enable
using IVC.Application.Redeterminaciones.Dtos;

namespace IVC.Services.Abstractions;

/// <summary>
/// Autorizantes y redeterminaciones virtuales (ex ListarAutorizantesYRedeterminaciones).
/// Alta, edición y baja con la planificación abierta; el seguimiento (expediente y estado)
/// lo edita cualquier usuario autenticado.
/// </summary>
public interface IAutorizantesService
{
    Task<IReadOnlyList<AutorizanteViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<AutorizanteViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AutorizanteViewModel> CreateAsync(AutorizanteViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(AutorizanteViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Edición inline de expediente y estado (PuedeEditarInline).</summary>
    Task ActualizarSeguimientoAsync(AutorizanteViewModel vm, CancellationToken ct = default);
}

/// <summary>Certificados y reliquidaciones virtuales (ex ListarCertificadosYReliquidaciones), con SIGAF prorrateado y SADE.</summary>
public interface ICertificadosService
{
    Task<IReadOnlyList<CertificadoViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<CertificadoViewModel> CreateAsync(CertificadoViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(CertificadoViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Edición inline: expediente (también en reliquidaciones, vía EXPEDIENTES_RELIQ), monto y ejecución física.</summary>
    Task ActualizarInlineAsync(CertificadoViewModel vm, CancellationToken ct = default);
}

/// <summary>Legítimos abonos con SIGAF y SADE.</summary>
public interface ILegitimosService
{
    Task<IReadOnlyList<LegitimoViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<LegitimoViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<LegitimoViewModel> CreateAsync(LegitimoViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(LegitimoViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Edición inline: expediente y monto certificado.</summary>
    Task ActualizarInlineAsync(LegitimoViewModel vm, CancellationToken ct = default);
}

/// <summary>Redeterminaciones (cross-area) con su autorizante, SADE y control de estados.</summary>
public interface IRedeterminacionesService
{
    Task<IReadOnlyList<RedeterminacionViewModel>> GetAllAsync(CancellationToken ct = default);
    Task<RedeterminacionViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RedeterminacionViewModel> CreateAsync(RedeterminacionViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(RedeterminacionViewModel vm, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Edición inline: etapa, expediente y usuario asignado.</summary>
    Task ActualizarSeguimientoAsync(RedeterminacionViewModel vm, CancellationToken ct = default);
}
