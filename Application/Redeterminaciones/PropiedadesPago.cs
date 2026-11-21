#nullable enable
namespace IVC.Application.Redeterminaciones;

/// <summary>Último pase SADE de un expediente. Buzon null significa que el expediente no tiene pases.</summary>
public sealed record UltimoPaseSade(string? Buzon, DateTime? Fecha)
{
    public static readonly UltimoPaseSade SinDatos = new(null, null);
    public bool TieneDatos => Buzon is not null;
}

/// <summary>
/// Estado y datos SADE de un pago (certificado, legítimo o reliquidación) a partir de los
/// datos ya cargados. Portado de CalculoRedeterminacionNegocioEF.CalcularPropiedadesAdicionales.
/// </summary>
public static class PropiedadesPago
{
    public static (string Estado, string? BuzonSade, DateTime? FechaSade) Calcular(
        string? expediente, decimal? sigaf, IReadOnlyDictionary<string, UltimoPaseSade>? datosSade)
    {
        var estado = ReglasRedeterminaciones.DeterminarEstado(expediente, sigaf);
        // Un pago sin expediente no busca pases (el diccionario no admite clave null: antes
        // caía en un catch y quedaba con estado "ERROR").
        var pase = datosSade is not null && datosSade.TryGetValue((expediente ?? string.Empty).Trim(), out var encontrado)
            ? encontrado
            : UltimoPaseSade.SinDatos;
        return (estado, pase.Buzon, pase.Fecha);
    }
}
