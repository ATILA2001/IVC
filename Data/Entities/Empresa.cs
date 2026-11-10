#nullable enable
namespace IVC.Data.Entities;

/// <summary>Empresa contratista. Tabla Empresas.</summary>
public class Empresa : ICatalogoSimple
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
}
