#nullable enable
using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;

namespace IVC.Application.Catalogos;

/// <summary>
/// Reglas de fila de los catálogos. Fuente única: las aplica el servicio (integridad,
/// no se pueden saltear) y las anticipa la grilla (aviso antes de cerrar la edición y
/// perder lo cargado). Los largos son los de las columnas varchar(255) de la base.
/// </summary>
public static class CatalogoSimpleValidator
{
    public const int NombreMaxLength = 255;

    public static IReadOnlyList<string> Validar(CatalogoSimpleViewModel vm)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(vm.Nombre))
            errores.Add("El nombre es obligatorio.");
        else
            Validaciones.Largo(errores, vm.Nombre.Trim(), NombreMaxLength, "El nombre");
        return errores;
    }
}

public static class LineaGestionValidator
{
    public const int TextoMaxLength = 255;

    public static IReadOnlyList<string> Validar(LineaGestionViewModel vm)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(vm.Nombre))
            errores.Add("El nombre es obligatorio.");
        else
            Validaciones.Largo(errores, vm.Nombre.Trim(), TextoMaxLength, "El nombre");
        Validaciones.Largo(errores, vm.Tipo?.Trim(), TextoMaxLength, "El tipo");
        Validaciones.Largo(errores, vm.Grupo?.Trim(), TextoMaxLength, "El grupo");
        Validaciones.Largo(errores, vm.Reparticion?.Trim(), TextoMaxLength, "La repartición");
        return errores;
    }
}

public static class LineaGestionFFValidator
{
    public const int TextoMaxLength = 255;

    public static IReadOnlyList<string> Validar(LineaGestionFFViewModel vm)
    {
        var errores = new List<string>();
        if (vm.LineaGestionId <= 0)
            errores.Add("Elegí la línea de gestión.");
        if (string.IsNullOrWhiteSpace(vm.Nombre))
            errores.Add("El nombre FF es obligatorio.");
        else
            Validaciones.Largo(errores, vm.Nombre.Trim(), TextoMaxLength, "El nombre FF");
        Validaciones.Largo(errores, vm.Fuente?.Trim(), TextoMaxLength, "La fuente");
        return errores;
    }
}

public static class ProyectoValidator
{
    public const int TextoMaxLength = 255;

    /// <summary>La obra se elige sólo en el alta: después la fila ya está atada a ella.</summary>
    public static IReadOnlyList<string> Validar(ProyectoViewModel vm, bool esAlta)
    {
        var errores = new List<string>();
        if (esAlta && vm.ObraId <= 0)
            errores.Add("Elegí la obra.");
        if (string.IsNullOrWhiteSpace(vm.Nombre))
            errores.Add("El proyecto es obligatorio.");
        else
            Validaciones.Largo(errores, vm.Nombre.Trim(), TextoMaxLength, "El proyecto");
        Validaciones.Largo(errores, vm.SubProyecto?.Trim(), TextoMaxLength, "El subproyecto");
        if (vm.LineaGestionId is null or <= 0)
            errores.Add("Elegí la línea de gestión.");
        if (vm.Autorizado2026 is null)
            errores.Add("El monto autorizado 2026 es obligatorio.");
        else
            ImporteReglas.PositivoConDosDecimales(errores, vm.Autorizado2026, "El monto autorizado 2026");
        return errores;
    }
}

public static class MovimientoValidator
{
    /// <summary>La obra se elige sólo en el alta: el movimiento no cambia de obra.</summary>
    public static IReadOnlyList<string> Validar(MovimientoViewModel vm, bool esAlta)
    {
        var errores = new List<string>();
        if (esAlta && vm.ObraId <= 0)
            errores.Add("Elegí la obra.");
        if (vm.Monto is null)
            errores.Add("El monto del movimiento es obligatorio.");
        else
            ImporteReglas.PositivoConDosDecimales(errores, vm.Monto, "El monto del movimiento");
        if (vm.Fecha is null)
            errores.Add("La fecha es obligatoria.");
        return errores;
    }
}

/// <summary>
/// Regla de los montos del legado ("sólo números positivos con hasta 2 decimales"):
/// se mantiene tal cual hasta que el área pida otra cosa.
/// </summary>
internal static class ImporteReglas
{
    public static void PositivoConDosDecimales(List<string> errores, decimal? valor, string campo)
    {
        Validaciones.ImporteNoNegativo(errores, valor, campo);
        if (valor is decimal v && decimal.Round(v, 2) != v)
            errores.Add($"{campo} admite hasta 2 decimales.");
    }
}
