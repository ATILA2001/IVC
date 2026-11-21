#nullable enable
using IVC.Application.Common;
using IVC.Application.Redeterminaciones.Dtos;

namespace IVC.Application.Redeterminaciones;

/// <summary>Reglas de fila de los cuatro formularios del módulo, las mismas que exigían los validadores del legado.</summary>
public static class AutorizanteValidator
{
    public const int DetalleMaxLength = 255;
    public const int ExpedienteMaxLength = 50;

    public static IReadOnlyList<string> Validar(AutorizanteViewModel vm, bool esAlta)
    {
        var errores = new List<string>();
        if (esAlta && vm.ObraId <= 0) errores.Add("Elegí la obra.");
        if (vm.ConceptoId is null or <= 0) errores.Add("Elegí el concepto.");
        if (vm.EstadoId is null or <= 0) errores.Add("Elegí el estado.");
        if (string.IsNullOrWhiteSpace(vm.Detalle)) errores.Add("El detalle es obligatorio.");
        else Validaciones.Largo(errores, vm.Detalle.Trim(), DetalleMaxLength, "El detalle");
        Validaciones.Largo(errores, vm.Expediente?.Trim(), ExpedienteMaxLength, "El expediente");
        if (vm.MontoAutorizado is null) errores.Add("El monto autorizado es obligatorio.");
        else Validaciones.ImportePositivoConDosDecimales(errores, vm.MontoAutorizado, "El monto autorizado");
        if (vm.MesAprobacion is null) errores.Add("El mes de aprobación es obligatorio.");
        return errores;
    }

    /// <summary>Edición inline de seguimiento: sólo el expediente tiene regla.</summary>
    public static IReadOnlyList<string> ValidarSeguimiento(AutorizanteViewModel vm)
    {
        var errores = new List<string>();
        Validaciones.Largo(errores, vm.Expediente?.Trim(), ExpedienteMaxLength, "El expediente");
        if (vm.EstadoId is null or <= 0) errores.Add("Elegí el estado.");
        return errores;
    }
}

public static class CertificadoValidator
{
    public const int ExpedienteMaxLength = 50;

    public static IReadOnlyList<string> Validar(CertificadoViewModel vm, bool esAlta)
    {
        var errores = new List<string>();
        if (esAlta && vm.AutorizanteId is null or <= 0) errores.Add("Elegí el autorizante.");
        if (vm.TipoPagoId is null or <= 0) errores.Add("Elegí el tipo de pago.");
        Validaciones.Largo(errores, vm.ExpedientePago?.Trim(), ExpedienteMaxLength, "El expediente");
        if (vm.MontoTotal is null) errores.Add("El monto certificado es obligatorio.");
        else Validaciones.ImportePositivoConDosDecimales(errores, vm.MontoTotal, "El monto certificado");
        if (vm.MesAprobacion is null) errores.Add("El mes de aprobación es obligatorio.");
        return errores;
    }

    /// <summary>Edición inline: expediente, monto y ejecución física.</summary>
    public static IReadOnlyList<string> ValidarInline(CertificadoViewModel vm)
    {
        var errores = new List<string>();
        Validaciones.Largo(errores, vm.ExpedientePago?.Trim(), ExpedienteMaxLength, "El expediente");
        if (!vm.EsReliquidacion)
        {
            if (vm.MontoTotal is null) errores.Add("El monto certificado es obligatorio.");
            else Validaciones.ImportePositivoConDosDecimales(errores, vm.MontoTotal, "El monto certificado");
            if (vm.PorcEjecFisica is < 0 or > 100) errores.Add("La ejecución física debe estar entre 0 y 100.");
        }
        return errores;
    }
}

public static class LegitimoValidator
{
    public const int CodigoMaxLength = 50;
    public const int ExpedienteMaxLength = 50;

    public static IReadOnlyList<string> Validar(LegitimoViewModel vm)
    {
        var errores = new List<string>();
        if (vm.ObraId <= 0) errores.Add("Elegí la obra.");
        if (string.IsNullOrWhiteSpace(vm.CodigoAutorizante)) errores.Add("El código de autorizante es obligatorio.");
        else Validaciones.Largo(errores, vm.CodigoAutorizante.Trim(), CodigoMaxLength, "El código de autorizante");
        if (string.IsNullOrWhiteSpace(vm.Expediente)) errores.Add("El expediente es obligatorio.");
        else Validaciones.Largo(errores, vm.Expediente.Trim(), ExpedienteMaxLength, "El expediente");
        if (vm.InicioEjecucion is null) errores.Add("El inicio de ejecución es obligatorio.");
        if (vm.FinEjecucion is null) errores.Add("El fin de ejecución es obligatorio.");
        else if (vm.InicioEjecucion is DateTime inicio && vm.FinEjecucion < inicio) errores.Add("El fin de ejecución no puede ser anterior al inicio.");
        if (vm.Certificado is null) errores.Add("El monto certificado es obligatorio.");
        else Validaciones.ImportePositivoConDosDecimales(errores, vm.Certificado, "El monto certificado");
        if (vm.MesAprobacion is null) errores.Add("El mes de aprobación es obligatorio.");
        return errores;
    }

    /// <summary>Edición inline: expediente y monto certificado.</summary>
    public static IReadOnlyList<string> ValidarInline(LegitimoViewModel vm)
    {
        var errores = new List<string>();
        Validaciones.Largo(errores, vm.Expediente?.Trim(), ExpedienteMaxLength, "El expediente");
        if (vm.Certificado is null) errores.Add("El monto certificado es obligatorio.");
        else Validaciones.ImportePositivoConDosDecimales(errores, vm.Certificado, "El monto certificado");
        return errores;
    }
}

public static class RedeterminacionValidator
{
    public const int ExpedienteMaxLength = 50;
    public const int TipoMaxLength = 50;
    public const int ObservacionesMaxLength = 255;
    /// <summary>PORCENTAJE_PONDERACION es decimal(5,2): admite signo (hay negativos en producción).</summary>
    public const decimal PorcentajeMaximo = 999.99m;

    public static IReadOnlyList<string> Validar(RedeterminacionViewModel vm, bool esAlta)
    {
        var errores = new List<string>();
        if (esAlta && vm.AutorizanteId is null or <= 0) errores.Add("Elegí el autorizante.");
        if (string.IsNullOrWhiteSpace(vm.Expediente)) errores.Add("El expediente es obligatorio.");
        else Validaciones.Largo(errores, vm.Expediente.Trim(), ExpedienteMaxLength, "El expediente");
        if (vm.Salto is null) errores.Add("El salto es obligatorio.");
        if (vm.Nro is null) errores.Add("El número es obligatorio.");
        if (string.IsNullOrWhiteSpace(vm.Tipo)) errores.Add("El tipo es obligatorio.");
        else Validaciones.Largo(errores, vm.Tipo.Trim(), TipoMaxLength, "El tipo");
        if (vm.EtapaId is null or <= 0) errores.Add("Elegí la etapa.");
        if (vm.Porcentaje is null) errores.Add("El porcentaje es obligatorio.");
        else if (Math.Abs(vm.Porcentaje.Value) > PorcentajeMaximo || decimal.Round(vm.Porcentaje.Value, 2) != vm.Porcentaje.Value)
            errores.Add("El porcentaje admite hasta 2 decimales y 3 enteros.");
        Validaciones.Largo(errores, vm.Observaciones?.Trim(), ObservacionesMaxLength, "Las observaciones");
        return errores;
    }

    /// <summary>Edición inline: etapa, expediente y usuario.</summary>
    public static IReadOnlyList<string> ValidarSeguimiento(RedeterminacionViewModel vm)
    {
        var errores = new List<string>();
        if (vm.EtapaId is null or <= 0) errores.Add("Elegí la etapa.");
        Validaciones.Largo(errores, vm.Expediente?.Trim(), ExpedienteMaxLength, "El expediente");
        return errores;
    }
}
