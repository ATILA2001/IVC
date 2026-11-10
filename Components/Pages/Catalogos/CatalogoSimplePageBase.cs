#nullable enable
using IVC.Application.Catalogos;
using IVC.Application.Catalogos.Dtos;
using IVC.Data.Entities;
using IVC.Services.Abstractions;
using Microsoft.AspNetCore.Components;

namespace IVC.Components.Pages.Catalogos;

/// <summary>
/// Base de las páginas de catálogo de sólo nombre (Barrios, Empresas): la misma grilla
/// con edición inline sobre el servicio genérico. Cada página aporta su ruta, títulos
/// y textos; el ciclo alta/edición/baja con permisos, confirmación y auditoría es el
/// de <see cref="GridPageBase{TItem}"/>.
/// </summary>
public abstract class CatalogoSimplePageBase<TEntity> : GridPageBase<CatalogoSimpleViewModel>
    where TEntity : class, ICatalogoSimple, new()
{
    [Inject] protected ICatalogoSimpleService<TEntity> Servicio { get; set; } = null!;

    protected override async Task<List<CatalogoSimpleViewModel>> ObtenerDatosAsync() =>
        (await Servicio.GetAllAsync()).ToList();

    protected override IEnumerable<string?> CamposBusqueda(CatalogoSimpleViewModel item) => [item.Nombre];

    protected override void PrepararParaGuardar(CatalogoSimpleViewModel item) =>
        item.Nombre = item.Nombre.Trim();

    protected override IReadOnlyList<string> Validar(CatalogoSimpleViewModel item, bool esAlta)
    {
        var errores = CatalogoSimpleValidator.Validar(item).ToList();
        // Duplicado contra lo ya cargado: el servicio revalida contra la base, pero
        // frenar acá deja la fila abierta en vez de cerrarla y perder lo tipeado. Se
        // excluye por Id y no por referencia: lo que llega es el buffer (una copia).
        if (_items.Any(x => x.Id != item.Id
                && string.Equals(x.Nombre.Trim(), item.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
            errores.Add($"Ya existe \"{item.Nombre.Trim()}\" en {TituloEntidad}.");
        return errores;
    }

    protected override int? IdAuditoria(CatalogoSimpleViewModel item) => item.Id != 0 ? item.Id : null;
    protected override string ClaveAuditoria(CatalogoSimpleViewModel item) => item.Nombre;

    protected override async Task CrearAsync(CatalogoSimpleViewModel item)
    {
        // El Id del alta vuelve a la fila en pantalla: lo usan el historial y una
        // edición o baja inmediatas sin recargar.
        var creado = await Servicio.CreateAsync(item);
        item.Id = creado.Id;
    }

    protected override Task ActualizarAsync(CatalogoSimpleViewModel item) => Servicio.UpdateAsync(item);

    protected override Task EliminarAsync(CatalogoSimpleViewModel item) =>
        item.Id != 0 ? Servicio.DeleteAsync(item.Id) : Task.CompletedTask;
}
