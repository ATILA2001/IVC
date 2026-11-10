#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Catálogo de ID + NOMBRE (BARRIOS, EMPRESAS). Permite un único repositorio y
/// servicio genéricos para las páginas que sólo administran nombres.
/// </summary>
public interface ICatalogoSimple
{
    int Id { get; set; }
    string? Nombre { get; set; }
}
