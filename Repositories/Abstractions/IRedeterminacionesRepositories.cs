#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

/// <summary>AUTORIZANTES con su obra y catálogos. El código lo asigna un trigger en el INSERT.</summary>
public interface IAutorizanteRepository
{
    /// <summary>Autorizantes de obras de las áreas indicadas, o todos si es null. Con Obra (y sus catálogos y proyecto), Estado y Concepto.</summary>
    Task<IReadOnlyList<Autorizante>> GetAllAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default);
    Task<Autorizante?> GetByIdAsync(int id, CancellationToken ct = default);
    /// <summary>Autorizantes de los códigos indicados, con obra y catálogos.</summary>
    Task<IReadOnlyList<Autorizante>> GetPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct = default);
    /// <summary>Inserta y relee el código que asignó el trigger TRG_Autorizantes_Insert.</summary>
    Task AddAsync(Autorizante entity, CancellationToken ct = default);
    Task UpdateAsync(Autorizante entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>True si hay certificados o redeterminaciones con ese código (no se puede borrar).</summary>
    Task<bool> TieneDependenciasAsync(string codigoAutorizante, CancellationToken ct = default);
    /// <summary>Suma de MONTO_TOTAL de los certificados del código (tope inferior del monto autorizado).</summary>
    Task<decimal> SumaCertificadosAsync(string codigoAutorizante, CancellationToken ct = default);
    /// <summary>Edición inline de seguimiento: sólo expediente y estado. False si la fila ya no existe.</summary>
    Task<bool> UpdateSeguimientoAsync(int id, string? expediente, int? estadoId, CancellationToken ct = default);
}

/// <summary>CERTIFICADOS, vinculados al autorizante por CODIGO_AUTORIZANTE.</summary>
public interface ICertificadoRepository
{
    /// <summary>Certificados de los códigos indicados, con su tipo de pago.</summary>
    Task<IReadOnlyList<Certificado>> GetPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct = default);
    Task<Certificado?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Certificado entity, CancellationToken ct = default);
    Task UpdateAsync(Certificado entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Suma de PORC_EJEC_FISICA por código de autorizante (sólo certificados con porcentaje cargado).</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetEjecFisicaPorCodigoAsync(IReadOnlyCollection<string> codigos, CancellationToken ct = default);
}

/// <summary>LEGITIMOS_ABONOS con su obra y catálogos.</summary>
public interface ILegitimoRepository
{
    Task<IReadOnlyList<Legitimo>> GetAllAsync(IReadOnlyCollection<int>? areaIds, CancellationToken ct = default);
    Task<Legitimo?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Legitimo entity, CancellationToken ct = default);
    Task UpdateAsync(Legitimo entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>REDETERMINACIONES con su etapa y su usuario. Cross-area por diseño (decisión del 01/11/2026).</summary>
public interface IRedeterminacionRepository
{
    Task<IReadOnlyList<Redeterminacion>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Redeterminacion>> GetPorCodigosAsync(IReadOnlyCollection<string> codigosAutorizante, CancellationToken ct = default);
    Task<Redeterminacion?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Redeterminacion entity, CancellationToken ct = default);
    Task UpdateAsync(Redeterminacion entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <summary>Edición inline: etapa, expediente o usuario (null = no cambiar; para el usuario, borrarUsuario lo desasigna).</summary>
    Task<bool> UpdateSeguimientoAsync(int id, int? etapaId, string? expediente, int? usuarioId, bool borrarUsuario, CancellationToken ct = default);
}

/// <summary>EXPEDIENTES_RELIQ: expediente de pago de una reliquidación virtual (código de redeterminación + mes).</summary>
public interface IExpedienteReliqRepository
{
    Task<IReadOnlyList<ExpedienteReliq>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Crea o actualiza el expediente del (código, mes); con expediente vacío lo deja vacío.</summary>
    Task GuardarOActualizarAsync(string codigoRedet, DateTime mesAprobacion, string? expediente, CancellationToken ct = default);
}
