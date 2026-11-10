#nullable enable
using IVC.Data.Entities;

namespace IVC.Repositories.Abstractions;

/// <summary>Atributos IVC de los usuarios de Auth.Web (tabla UsuariosVinculados).</summary>
public interface IUsuarioVinculadoRepository
{
    /// <summary>
    /// Devuelve la fila del usuario; si no existe la crea (primer ingreso) y si el nombre
    /// cambió lo sincroniza. El nombre se recorta al largo de la columna.
    /// </summary>
    Task<UsuarioVinculado> GetOrCreateAsync(string authUserId, string nombre, CancellationToken ct = default);

    Task<IReadOnlyList<UsuarioVinculado>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Cambia el override de planificación. False si el usuario ya no existe.</summary>
    Task<bool> SetPlanificacionAbiertaAsync(string authUserId, bool abierta, CancellationToken ct = default);
}
