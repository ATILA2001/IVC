#nullable enable

namespace IVC.Application.Common;

/// <summary>
/// Utilidades comunes de los validadores de fila.
/// </summary>
public static class Validaciones
{
    /// <summary>
    /// Valida el largo contra el MaxLength de la columna: sin esto, el exceso llega a
    /// SQL Server y vuelve como error de truncamiento en inglés.
    /// </summary>
    public static void Largo(List<string> errores, string? valor, int maximo, string campo)
    {
        if (valor is not null && valor.Length > maximo)
            errores.Add($"{campo} supera los {maximo} caracteres.");
    }


    /// <summary>Valida que un importe opcional, si viene cargado, no sea negativo.</summary>
    public static void ImporteNoNegativo(List<string> errores, decimal? valor, string campo)
    {
        if (valor is < 0)
            errores.Add($"{campo} no puede ser negativo.");
    }
}