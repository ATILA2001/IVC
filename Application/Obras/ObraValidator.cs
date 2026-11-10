#nullable enable
using IVC.Application.Common;
using IVC.Application.Obras.Dtos;

namespace IVC.Application.Obras;

/// <summary>
/// Reglas de la fila de obra: las mismas que exigía el formulario del legado (todos los
/// campos obligatorios y numéricos) más el largo de la columna DESCRIPCION.
/// </summary>
public static class ObraValidator
{
    public const int DescripcionMaxLength = 255;
    public const int AnioMinimo = 1900;
    public const int AnioMaximo = 2100;

    public static IReadOnlyList<string> Validar(ObraViewModel vm)
    {
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(vm.Descripcion))
            errores.Add("El nombre de la obra es obligatorio.");
        else
            Validaciones.Largo(errores, vm.Descripcion.Trim(), DescripcionMaxLength, "El nombre de la obra");
        if (vm.AreaId <= 0) errores.Add("Elegí el área.");
        if (vm.EmpresaId <= 0) errores.Add("Elegí la empresa.");
        if (vm.ContrataId is null or <= 0) errores.Add("Elegí la contrata.");
        if (vm.BarrioId is null or <= 0) errores.Add("Elegí el barrio.");
        if (vm.Numero is null) errores.Add("El número es obligatorio.");
        if (vm.Anio is null) errores.Add("El año es obligatorio.");
        else if (vm.Anio < AnioMinimo || vm.Anio > AnioMaximo) errores.Add($"El año debe estar entre {AnioMinimo} y {AnioMaximo}.");
        if (vm.Etapa is null) errores.Add("La etapa es obligatoria.");
        if (vm.ObraNumero is null) errores.Add("El número de obra es obligatorio.");
        return errores;
    }
}
