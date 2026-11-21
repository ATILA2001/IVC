#nullable enable
using IVC.Application.Identidad;
using IVC.Application.Redeterminaciones;
using IVC.Data.Entities;

namespace IVC.Services.Implementations;

/// <summary>Mapeos que comparten los cuatro servicios del módulo de redeterminaciones.</summary>
internal static class RedeterminacionesMapeo
{
    /// <summary>"Contrata número/año" como lo mostraban las grillas del legado; null sin contrata.</summary>
    public static string? TextoContrata(Obra? o)
    {
        var nombre = o?.Contrata?.Nombre;
        if (o is null || string.IsNullOrWhiteSpace(nombre)) return null;
        return o.Numero is not null && o.Anio is not null ? $"{nombre} {o.Numero}/{o.Anio}" : nombre;
    }

    /// <summary>
    /// Áreas con las que se filtra la grilla: null para quien ve todo; vacío (fail-closed)
    /// para quien no tiene áreas resueltas.
    /// </summary>
    public static IReadOnlyCollection<int>? AreasVisibles(UsuarioIvc usuario, bool veTodas) =>
        veTodas ? null : ReglasPermisosIvc.AreaIdsParaFiltro(usuario);

    /// <summary>Expedientes distintos con dato, tal cual están cargados (las tablas externas se consultan con el mismo texto).</summary>
    public static List<string> Expedientes(IEnumerable<string?> valores) =>
        valores.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!).Distinct().ToList();

    public static AutorizanteCalculo? ToCalculo(Autorizante a) =>
        string.IsNullOrEmpty(a.CodigoAutorizante) ? null : new AutorizanteCalculo(a.CodigoAutorizante, a.MontoAutorizado ?? 0);

    public static RedeterminacionCalculo ToCalculo(Redeterminacion r) => new()
    {
        Id = r.Id,
        CodigoAutorizante = r.CodigoAutorizante,
        Nro = r.Nro,
        Porcentaje = r.Porcentaje,
        Salto = r.Salto,
        EtapaId = r.EtapaId,
    };

    /// <summary>Certificado con su porcentaje sobre el monto autorizado del autorizante (lo necesita GenerarReliquidaciones).</summary>
    public static CertificadoCalculo ToCalculo(Certificado c, decimal? montoAutorizado) => new(
        c.Id, c.CodigoAutorizante, c.MontoTotal ?? 0, c.MesAprobacion, c.TipoPagoId,
        ReglasRedeterminaciones.PorcentajeSobreAutorizado(c.MontoTotal ?? 0, montoAutorizado ?? 0));

    /// <summary>MontoRedet y PorcentajeEjecutadoPreSalto de cada redeterminación, por Id.</summary>
    public static Dictionary<int, RedeterminacionCalculo> CalcularMontos(
        IEnumerable<Redeterminacion> redeterminaciones, IEnumerable<Autorizante> autorizantes, IEnumerable<CertificadoCalculo> certificados)
    {
        var calculos = redeterminaciones.Select(ToCalculo).ToList();
        ReglasRedeterminaciones.CalcularMontos(calculos, autorizantes.Select(ToCalculo).OfType<AutorizanteCalculo>(), certificados);
        return calculos.ToDictionary(c => c.Id);
    }

    public static string? ErrorDeValidacion(IReadOnlyList<string> errores) => errores.Count > 0 ? string.Join(" ", errores) : null;

    public static readonly IReadOnlyDictionary<string, UltimoPaseSade> SinSade = new Dictionary<string, UltimoPaseSade>();
    public static readonly IReadOnlyDictionary<string, string> SinAcdir = new Dictionary<string, string>();
    public static readonly IReadOnlyDictionary<string, decimal> SinImportes = new Dictionary<string, decimal>();
}
