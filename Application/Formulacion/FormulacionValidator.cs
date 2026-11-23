#nullable enable
using IVC.Application.Common;
using IVC.Application.Formulacion.Dtos;

namespace IVC.Application.Formulacion;

/// <summary>
/// Reglas del formulario de formulación del legado: obra (sólo en el alta), al menos un
/// monto en alguno de los años del ciclo, montos positivos con hasta 2 decimales, unidad
/// de medida, valor de medida y prioridad obligatorios, y los largos de las dos columnas
/// de texto (que el negocio del legado rechazaba con InvalidOperationException). El PPI y
/// el techo se validan sólo cuando el usuario los edita (administrador): el resto ni los ve
/// ni los persiste, así que un valor heredado no puede trabarle el guardado.
/// </summary>
public static class FormulacionValidator
{
    public const int BreveDescripcionMaxLength = 500;
    public const int ObservacionesMaxLength = 255;

    public static IReadOnlyList<string> Validar(FormulacionViewModel vm, bool esAlta, bool editaPpiYTecho = true)
    {
        var errores = new List<string>();
        if (esAlta && vm.ObraId <= 0) errores.Add("Elegí la obra.");

        if (vm.Montos.All(m => m is null))
            errores.Add("Cargá al menos un monto para alguno de los años.");
        Validaciones.ImportePositivoConDosDecimales(errores, vm.MontoAnio1, "El monto del primer año");
        Validaciones.ImportePositivoConDosDecimales(errores, vm.MontoAnio2, "El monto del segundo año");
        Validaciones.ImportePositivoConDosDecimales(errores, vm.MontoAnio3, "El monto del tercer año");

        if (editaPpiYTecho)
        {
            if (vm.Ppi is < 0) errores.Add("El PPI no puede ser negativo.");
            Validaciones.ImportePositivoConDosDecimales(errores, vm.Techos, "El techo");
        }

        if (vm.UnidadMedidaId is null or <= 0) errores.Add("Elegí la unidad de medida.");
        if (vm.ValorMedida is null) errores.Add("El valor de medida es obligatorio.");
        else Validaciones.ImportePositivoConDosDecimales(errores, vm.ValorMedida, "El valor de medida");
        if (vm.PrioridadId is null or <= 0) errores.Add("Elegí la prioridad.");

        Validaciones.Largo(errores, vm.BreveDescripcion?.Trim(), BreveDescripcionMaxLength, "La breve descripción");
        Validaciones.Largo(errores, vm.Observaciones?.Trim(), ObservacionesMaxLength, "Las observaciones");
        return errores;
    }
}
