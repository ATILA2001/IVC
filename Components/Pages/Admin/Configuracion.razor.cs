#nullable enable
using IVC.Application.Identidad;
using IVC.Application.Identidad.Dtos;
using IVC.Security;
using IVC.Services.Abstractions;
using IVC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace IVC.Components.Pages.Admin;

/// <summary>
/// Panel de configuración (ex PanelConfiguracion.aspx): interruptores globales de
/// planificación y formulación y override de planificación por usuario. Sólo para
/// administradores: el rol se verifica al cargar y de nuevo en cada cambio.
/// </summary>
public partial class Configuracion
{
    [Inject] private IConfiguracionService Servicio { get; set; } = null!;
    [Inject] private INotificationHelper Notification { get; set; } = null!;
    [Inject] private IUsuarioActual UsuarioActual { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;
    [Inject] private ILogger<Configuracion> Logger { get; set; } = null!;

    private const string PageUrl = "/admin/configuracion";

    private bool _loading = true;
    private bool _guardando;
    private bool _esAdmin;
    private EstadoPlanificacion _estado = EstadoPlanificacion.Cerrado;
    private List<UsuarioVinculadoViewModel> _usuarios = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadPermissionsAsync(PageUrl);
        _esAdmin = await EsAdminAsync();
        if (!_esAdmin)
        {
            _loading = false;
            return;
        }

        try
        {
            _estado = await Servicio.ObtenerEstadoAsync();
            await CargarUsuariosAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "No se pudo cargar la configuración de planificación.");
            Notification.ShowError("No se pudo cargar la configuración. Recargá la página.", "Error al cargar");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CambiarPlanificacion(bool abierta)
    {
        await Guardar("la planificación", async () =>
        {
            await Servicio.SetPlanificacionAbiertaAsync(abierta, NombreUsuario);
            _estado = _estado with { PlanificacionAbierta = abierta };
            // Al cerrar vuelven a importar los overrides: se muestra la lista actualizada.
            if (!abierta) await CargarUsuariosAsync();
        });
    }

    private async Task CambiarFormulacion(bool abierta)
    {
        await Guardar("la formulación", async () =>
        {
            await Servicio.SetFormulacionAbiertaAsync(abierta, NombreUsuario);
            _estado = _estado with { FormulacionAbierta = abierta };
        });
    }

    private async Task CambiarOverride(UsuarioVinculadoViewModel usuario, bool abierta)
    {
        await Guardar($"la habilitación de {usuario.Nombre}", async () =>
        {
            await Servicio.SetOverridePlanificacionAsync(usuario.AuthUserId, abierta, NombreUsuario);
            usuario.PlanificacionAbierta = abierta;
            // Si el administrador se cambió a sí mismo, su usuario cacheado quedó viejo.
            var actual = await UsuarioActual.ObtenerAsync();
            if (string.Equals(actual.AuthUserId, usuario.AuthUserId, StringComparison.Ordinal))
                UsuarioActual.Invalidar();
        });
    }

    /// <summary>Revalida el rol en el servidor, ejecuta el cambio y traduce el resultado a una notificación.</summary>
    private async Task Guardar(string que, Func<Task> accion)
    {
        if (!await EsAdminAsync())
        {
            Notification.ShowError("Sólo un administrador puede cambiar la configuración.", "Permiso denegado");
            return;
        }

        _guardando = true;
        try
        {
            await accion();
            Notification.ShowSuccess($"Se guardó {que}.", "Configuración");
        }
        catch (Application.Common.ConflictoDeConcurrenciaException ex)
        {
            Notification.ShowWarning(ex.Message, "Conflicto");
            await RecargarAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error al guardar {Que} en {Pagina}.", que, PageUrl);
            Notification.ShowError($"No se pudo guardar {que}. Se recargó el estado actual.", "Error al guardar");
            await RecargarAsync();
        }
        finally
        {
            _guardando = false;
        }
    }

    private async Task RecargarAsync()
    {
        try
        {
            _estado = await Servicio.ObtenerEstadoAsync();
            await CargarUsuariosAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "No se pudo recargar la configuración tras un error.");
        }
    }

    private async Task CargarUsuariosAsync()
        => _usuarios = (await Servicio.ListarUsuariosAsync()).ToList();

    private async Task<bool> EsAdminAsync()
        => AdminClaims.IsAdmin((await AuthStateProvider.GetAuthenticationStateAsync()).User);
}
