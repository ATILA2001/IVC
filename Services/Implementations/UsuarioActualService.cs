#nullable enable
using IVC.Application.Identidad;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace IVC.Services.Implementations;

/// <summary>
/// Caché por circuito del usuario IVC. Se reutiliza sólo mientras la identidad efectiva
/// de la cookie (persona, roles, áreas, versión de permisos) sea la misma; ante cualquier
/// cambio se vuelve a construir, igual que hacía el legado con Session["Usuario"].
/// </summary>
public sealed class UsuarioActualService(
    AuthenticationStateProvider authenticationState,
    IUsuarioIvcFactory factory) : IUsuarioActual
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private UsuarioIvc? _usuario;
    private string? _clave;

    public async Task<UsuarioIvc> ObtenerAsync(CancellationToken ct = default)
    {
        var principal = (await authenticationState.GetAuthenticationStateAsync()).User;
        var clave = factory.ClaveDeIdentidad(principal);

        if (_usuario is not null && string.Equals(_clave, clave, StringComparison.Ordinal))
            return _usuario;

        // Varias operaciones del mismo circuito pueden pedir el usuario a la vez (carga
        // progresiva de grillas): una sola construye, las demás esperan.
        await _candado.WaitAsync(ct);
        try
        {
            if (_usuario is not null && string.Equals(_clave, clave, StringComparison.Ordinal))
                return _usuario;

            _usuario = await factory.ConstruirAsync(principal, ct);
            _clave = clave;
            return _usuario;
        }
        finally
        {
            _candado.Release();
        }
    }

    public void Invalidar()
    {
        _usuario = null;
        _clave = null;
    }
}
