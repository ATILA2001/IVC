#nullable enable
using System.Security.Claims;
using IVC.Application.Identidad;
using IVC.Repositories.Abstractions;
using IVC.Security;
using IVC.Services.Abstractions;

namespace IVC.Services.Implementations;

public sealed class UsuarioIvcFactory(
    IAreaRepository areas,
    IUsuarioVinculadoRepository vinculados,
    ILogger<UsuarioIvcFactory> logger) : IUsuarioIvcFactory
{
    private const string ClaimArea = "area";
    private const string ClaimPermsVersion = "perms_version";

    public async Task<UsuarioIvc> ConstruirAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return UsuarioIvc.Anonimo;

        var authUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var nombre = principal.FindFirstValue(ClaimTypes.Name);
        var correo = principal.FindFirstValue(ClaimTypes.Email);
        var mostrar = !string.IsNullOrWhiteSpace(nombre) ? nombre! : (correo ?? "Usuario");
        var esAdmin = AdminClaims.IsAdmin(principal);

        var authAreaIds = principal.Claims
            .Where(c => string.Equals(c.Type, ClaimArea, StringComparison.OrdinalIgnoreCase))
            .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
            .OfType<int>()
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        IReadOnlyList<int> ivcAreaIds = Array.Empty<int>();
        int? areaPrincipalId = null;
        string? areaPrincipalNombre = null;
        var planificacionOverride = false;

        try
        {
            if (authAreaIds.Count > 0)
            {
                var ivcAreas = await areas.GetByAuthAreaIdsAsync(authAreaIds, ct);
                ivcAreaIds = ivcAreas.Select(a => a.Id).ToList();
                var principalArea = ivcAreas.FirstOrDefault();
                areaPrincipalId = principalArea?.Id;
                areaPrincipalNombre = principalArea?.Nombre;
            }

            if (!string.IsNullOrWhiteSpace(authUserId))
            {
                var vinculado = await vinculados.GetOrCreateAsync(authUserId, mostrar, ct);
                planificacionOverride = vinculado.PlanificacionAbierta;
            }
        }
        catch (Exception ex)
        {
            // Sin la base no se puede saber qué puede hacer el usuario: no se adivina.
            logger.LogError(ex, "No se pudo resolver el usuario {AuthUserId} contra la base IVC (áreas {Areas}).",
                authUserId, string.Join(",", authAreaIds));
            throw new InvalidOperationException("No se pudo resolver el usuario actual contra la base de datos de IVC.", ex);
        }

        return new UsuarioIvc(
            Autenticado: true,
            AuthUserId: authUserId,
            Nombre: mostrar,
            Correo: correo,
            EsAdmin: esAdmin,
            AuthAreaIds: authAreaIds,
            IvcAreaIds: ivcAreaIds,
            AreaPrincipalId: areaPrincipalId,
            AreaPrincipalNombre: areaPrincipalNombre,
            PlanificacionAbiertaOverride: planificacionOverride);
    }

    public string ClaveDeIdentidad(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return string.Empty;

        string Unir(string tipo) => string.Join(",", principal.Claims
            .Where(c => string.Equals(c.Type, tipo, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .OrderBy(v => v, StringComparer.Ordinal));

        return string.Join("|",
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Unir(ClaimTypes.Role),
            Unir(ClaimArea),
            principal.FindFirstValue(ClaimPermsVersion) ?? string.Empty);
    }
}
