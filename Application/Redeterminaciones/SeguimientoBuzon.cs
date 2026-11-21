#nullable enable
namespace IVC.Application.Redeterminaciones;

/// <summary>
/// "Días en buzón" de la grilla de Redeterminaciones: cuántos días lleva el expediente en
/// el último buzón de SADE. Sólo cuenta para las etapas en trámite; las terminales
/// (aprobadas, desestimadas, no iniciadas y la 34) no muestran días. Portado del
/// markup y del code-behind de RedeterminacionesEF.aspx.
/// </summary>
public static class SeguimientoBuzon
{
    /// <summary>Etapas que no se siguen por días en buzón: 12, 22, 33, 34, 35, 36, 37, 38, 39.</summary>
    public static readonly IReadOnlyList<int> EtapasSinSeguimiento = [12, 22, 33, 34, 35, 36, 37, 38, 39];

    /// <summary>Rangos del filtro "Días x buzón" del legado.</summary>
    public enum Rango { Todos, MenosDeSiete, DeSieteACatorce, MasDeCatorce }

    /// <summary>Días completos desde el último pase, o null si no hay pase, no hay etapa o la etapa no se sigue.</summary>
    public static int? DiasEnBuzon(DateTime? fechaSade, int? etapaId, DateTime hoy)
    {
        if (fechaSade is null || etapaId is null || EtapasSinSeguimiento.Contains(etapaId.Value))
            return null;
        return (int)Math.Floor((hoy - fechaSade.Value).TotalDays);
    }

    /// <summary>Semáforo del legado: verde menos de 7, naranja de 7 a 14, rojo más de 14.</summary>
    public static Rango RangoDe(int dias) => dias < 7 ? Rango.MenosDeSiete : dias <= 14 ? Rango.DeSieteACatorce : Rango.MasDeCatorce;

    public static bool CumpleRango(int? dias, Rango rango) =>
        rango == Rango.Todos || (dias is int d && RangoDe(d) == rango);
}
