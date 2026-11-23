#nullable enable

namespace IVC.Application.Formulacion;

/// <summary>
/// Configuración del módulo (sección "Formulacion" de appsettings). El año base se cambia
/// para "iniciar un nuevo ciclo" sin recompilar, como la clave FormulacionAnioBase del legado.
/// </summary>
public sealed class FormulacionOptions
{
    public const string Seccion = "Formulacion";
    public const int AnioBasePorDefecto = 2027;

    public int AnioBase { get; set; } = AnioBasePorDefecto;
}

/// <summary>
/// Ciclo de formulación vigente (ex Negocio.FormulacionCiclo): el año base y los tres
/// años consecutivos para los que se carga el monto estimado de cada obra (base, base+1,
/// base+2). Las formulaciones de años fuera de la ventana no se muestran ni se tocan.
/// </summary>
public sealed record FormulacionCiclo(int AnioBase)
{
    public const int CantidadAnios = 3;

    public static FormulacionCiclo Desde(FormulacionOptions? opciones) =>
        new(opciones is { AnioBase: > 0 } o ? o.AnioBase : FormulacionOptions.AnioBasePorDefecto);

    /// <summary>Los 3 años del ciclo: [base, base+1, base+2].</summary>
    public int[] Anios => [AnioBase, AnioBase + 1, AnioBase + 2];

    /// <summary>Fecha que se persiste en FECHA_PERIODO para un año del ciclo (1 de enero).</summary>
    public static DateTime FechaPeriodo(int anio) => new(anio, 1, 1);

    /// <summary>Posición (0..2) del año dentro del ciclo, o null si queda fuera.</summary>
    public int? Posicion(int anio)
    {
        var posicion = anio - AnioBase;
        return posicion is >= 0 and < CantidadAnios ? posicion : null;
    }

    public bool Contiene(int anio) => Posicion(anio) is not null;
}
