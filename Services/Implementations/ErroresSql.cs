#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IVC.Services.Implementations;

/// <summary>
/// Traduce el error de SQL Server detrás de un DbUpdateException a un mensaje para el
/// usuario (portado de AutorizanteNegocioEF.MensajeParaErrorSql). Los RAISERROR de los
/// triggers de AUTORIZANTES y CERTIFICADOS ya vienen redactados para el usuario y se
/// devuelven tal cual. Null si el error no es de SQL Server o no tiene traducción.
/// </summary>
internal static class ErroresSql
{
    private const int ErrorDeTrigger = 50000;

    public static string? MensajeParaElUsuario(DbUpdateException ex) =>
        ex.GetBaseException() is SqlException sql
            ? sql.Number switch
            {
                ErrorDeTrigger => sql.Message,
                2601 or 2627 => "Ya existe un registro con ese código.",
                547 => "No se pudo guardar: alguno de los valores seleccionados no es válido o el registro está relacionado con otros datos.",
                8152 or 2628 => "Alguno de los textos ingresados supera el largo permitido.",
                _ => null,
            }
            : null;

    /// <summary>Relanza como ArgumentException (mensaje visible) si hay traducción; si no, deja pasar la excepción original.</summary>
    public static Exception Traducir(DbUpdateException ex) =>
        MensajeParaElUsuario(ex) is string mensaje ? new ArgumentException(mensaje, ex) : ex;
}
