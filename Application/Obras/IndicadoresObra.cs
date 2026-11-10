#nullable enable
namespace IVC.Application.Obras;

/// <summary>
/// Agregados de una obra tal como salen de la base: sumas de autorizantes, legítimos y
/// certificados, fechas extremas y, para la ejecución física, el monto autorizado de
/// cada autorizante con la suma de porcentajes de sus certificados.
/// </summary>
public sealed record AgregadosObra(
    decimal SumaAutorizantes,
    decimal SumaAutorizantesConcepto4,
    decimal SumaLegitimos,
    DateTime? MinLegitimo,
    DateTime? MaxLegitimo,
    decimal SumaCertificados,
    DateTime? MinCertificado,
    DateTime? MaxCertificado,
    IReadOnlyList<AgregadosObra.AutorizanteEjec> Autorizantes)
{
    /// <summary>Un autorizante de la obra: su peso (monto autorizado) y la ejecución física acumulada de sus certificados.</summary>
    public sealed record AutorizanteEjec(decimal MontoAutorizado, decimal EjecFisica);

    /// <summary>Obra sin autorizantes, legítimos ni certificados.</summary>
    public static readonly AgregadosObra Vacio = new(0m, 0m, 0m, null, null, 0m, null, null, Array.Empty<AutorizanteEjec>());
}

/// <summary>Indicadores financieros y físicos de una obra, listos para la grilla.</summary>
public sealed record IndicadoresObra(
    decimal MontoCertificado,
    decimal? Porcentaje,
    decimal MontoInicial,
    decimal MontoActual,
    decimal MontoFaltante,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    decimal? EjecFisica,
    decimal? FaltanteEjecFisica)
{
    /// <summary>Concepto de autorizante que define el monto de obra inicial (ID 4 en CONCEPTOS).</summary>
    public const int ConceptoMontoInicial = 4;

    /// <summary>
    /// Las fórmulas de CalculoObraNegocioEF.ObtenerFinanzasPorObras y
    /// ObtenerEjecFisicaBulkPorObras del legado, sin cambios:
    /// certificado = certificados + legítimos; inicial = autorizantes concepto 4 + legítimos;
    /// actual = autorizantes + legítimos; faltante = actual - certificado; porcentaje sobre el
    /// autorizado 2026 sólo si es mayor que cero; ejecución física = promedio de la ejecución
    /// de cada autorizante ponderado por su monto (sin autorizantes o con peso cero no hay dato).
    /// </summary>
    public static IndicadoresObra Calcular(AgregadosObra a, decimal? autorizado2026)
    {
        var montoCertificado = a.SumaCertificados + a.SumaLegitimos;
        var montoInicial = a.SumaAutorizantesConcepto4 + a.SumaLegitimos;
        var montoActual = a.SumaAutorizantes + a.SumaLegitimos;
        var montoFaltante = montoActual - montoCertificado;

        var fechaInicio = Minimo(a.MinCertificado, a.MinLegitimo);
        var fechaFin = Maximo(a.MaxCertificado, a.MaxLegitimo);

        decimal? porcentaje = autorizado2026 is > 0m ? montoCertificado / autorizado2026.Value * 100m : null;

        decimal? ejecFisica = null;
        var peso = a.Autorizantes.Sum(x => x.MontoAutorizado);
        if (a.Autorizantes.Count > 0 && peso != 0m)
            ejecFisica = a.Autorizantes.Sum(x => x.MontoAutorizado * x.EjecFisica) / peso;

        return new IndicadoresObra(
            montoCertificado, porcentaje, montoInicial, montoActual, montoFaltante,
            fechaInicio, fechaFin, ejecFisica, ejecFisica is decimal e ? 100m - e : null);
    }

    private static DateTime? Minimo(DateTime? x, DateTime? y) =>
        x is null ? y : y is null ? x : x < y ? x : y;

    private static DateTime? Maximo(DateTime? x, DateTime? y) =>
        x is null ? y : y is null ? x : x > y ? x : y;
}
