#nullable enable
using IVC.Application.Common;
using IVC.Application.Identidad;
using IVC.Application.Identidad.Dtos;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;

namespace IVC.Services.Implementations;

public sealed class ConfiguracionService(
    IAbmPlaniRepository abmPlani,
    IUsuarioVinculadoRepository vinculados,
    IAuditoriaService auditoria,
    ILogger<ConfiguracionService> logger) : IConfiguracionService
{
    /// <summary>Misma URL que la página: el historial se busca por vista.</summary>
    public const string Vista = "/admin/configuracion";

    public Task<EstadoPlanificacion> ObtenerEstadoAsync(CancellationToken ct = default) => abmPlani.GetAsync(ct);

    public async Task SetPlanificacionAbiertaAsync(bool abierta, string usuario, CancellationToken ct = default)
    {
        var actual = await abmPlani.GetAsync(ct);
        if (actual.PlanificacionAbierta == abierta) return;
        await abmPlani.SetAsync(actual with { PlanificacionAbierta = abierta }, ct);
        await AuditarAsync("Planificación", "Planificación abierta", actual.PlanificacionAbierta, abierta, usuario, ct);
    }

    public async Task SetFormulacionAbiertaAsync(bool abierta, string usuario, CancellationToken ct = default)
    {
        var actual = await abmPlani.GetAsync(ct);
        if (actual.FormulacionAbierta == abierta) return;
        await abmPlani.SetAsync(actual with { FormulacionAbierta = abierta }, ct);
        await AuditarAsync("Formulación", "Formulación abierta", actual.FormulacionAbierta, abierta, usuario, ct);
    }

    public async Task<IReadOnlyList<UsuarioVinculadoViewModel>> ListarUsuariosAsync(CancellationToken ct = default)
    {
        var filas = await vinculados.GetAllAsync(ct);
        return filas.Select(u => new UsuarioVinculadoViewModel
        {
            AuthUserId = u.AuthUserId,
            Nombre = u.Nombre,
            PlanificacionAbierta = u.PlanificacionAbierta,
        }).ToList();
    }

    public async Task SetOverridePlanificacionAsync(string authUserId, bool abierta, string usuario, CancellationToken ct = default)
    {
        var fila = (await vinculados.GetAllAsync(ct)).FirstOrDefault(u => u.AuthUserId == authUserId);
        if (fila is null)
            throw new ConflictoDeConcurrenciaException("El usuario ya no existe en IVC. Se recargó la lista.");
        if (fila.PlanificacionAbierta == abierta) return;

        if (!await vinculados.SetPlanificacionAbiertaAsync(authUserId, abierta, ct))
            throw new ConflictoDeConcurrenciaException("El usuario ya no existe en IVC. Se recargó la lista.");

        await AuditarAsync($"Usuario {fila.Nombre}", "Planificación habilitada", fila.PlanificacionAbierta, abierta, usuario, ct);
    }

    private async Task AuditarAsync(string clave, string campo, bool antes, bool despues, string usuario, CancellationToken ct)
    {
        try
        {
            await auditoria.RegistrarAsync(new RegistroAuditoria(
                Vista, null, clave, "Edición", usuario,
                [new AuditoriaDiff.Cambio(campo, SiNo(antes), SiNo(despues))]), ct);
        }
        catch (Exception ex)
        {
            // La auditoría nunca voltea el cambio que registra.
            logger.LogWarning(ex, "No se pudo auditar {Campo} de {Clave} en {Vista}.", campo, clave, Vista);
        }
    }

    private static string SiNo(bool valor) => valor ? "Sí" : "No";
}
