#nullable enable
namespace IVC.Data.Entities;

/// <summary>
/// Área del IVC. Tabla Areas. <see cref="AuthAreaId"/> correlaciona el claim "area" (entero)
/// de la cookie de Auth.Web con el área local; NULL significa área no mapeada.
/// </summary>
public class Area
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public int? AuthAreaId { get; set; }
}
