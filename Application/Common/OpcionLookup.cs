#nullable enable
namespace IVC.Application.Common;

/// <summary>Opción de un desplegable: el Id que se persiste y el texto que ve el usuario.</summary>
public sealed record OpcionLookup(int Id, string Nombre);
