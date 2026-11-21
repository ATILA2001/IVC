#nullable enable
namespace IVC.Application.Redeterminaciones;

/// <summary>
/// Correspondencia entre la etapa de una redeterminación (nombre en ESTADOS_REDET) y los buzones
/// de SADE en los que puede estar su expediente. La grilla de Redeterminaciones marca el pase
/// cuyo buzón no corresponde a la etapa. Portado del legado (archivo ISO-8859-1) a UTF-8: las
/// claves con tilde ("Análisis") se conservan tal cual para que sigan coincidiendo con la tabla.
/// </summary>
public static class BuzonEtapaMatcher
{
    private static readonly string[] BuzonesTecnica =
    [
        "IVC-2600 GO PLANIFICACION Y CONTROL",
        "IVC-3300 GO PLANEAMIENTO Y EVALUACION DE OBRAS",
        "IVC-3400 GO INSPECCION Y AUDITORIA DE OBRAS",
        "IVC-3000 DG DESARROLLO DE OBRAS",
        "IVC-3500 GO PROYECTOS URBANOS E INFRAESTRUCTURA",
        "IVC-2000 DG DESARROLLO HABITACIONAL",
        "IVC-2240 DEPTO OBRAS",
        "IVC-3430 DEPTO OBRAS1",
        "IVC-2400 GO CONJUNTOS URBANOS",
    ];

    private static readonly string[] BuzonesDgayf =
    [
        "IVC-4010 DEPTO REDETERMINACIONES",
        "IVC-4100 GO ECONOMICA FINANCIERA",
        "IVC-4000 DG ADMINISTRACION Y FINANZAS",
    ];

    private static readonly string[] BuzonesPresupuesto = ["IVC-4120 DEPTO PRESUPUESTOS", "IVC-12400 GO LOGISTICA"];
    private static readonly string[] BuzonesDispo = ["IVC-1850 DEPTO DESPACHOS Y PROYECTOS", "IVC-1400 DG TECNICO ADMINISTRATIVA Y LEGAL"];
    private static readonly string[] BuzonesDictamen = ["IVC-5200 GO ASUNTOS LEGALES", "IVC-1400 DG TECNICO ADMINISTRATIVA Y LEGAL", "IVC-5120 DEPTO ASUNTOS JUDICIALES"];
    private static readonly string[] Redeterminaciones = ["IVC-4010 DEPTO REDETERMINACIONES"];

    private static readonly IReadOnlyDictionary<string, string[]> Correspondencias = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["RD-01/11-Subsanacion Empresa"] = Redeterminaciones,
        ["RD-02/11-Análisis Tecnica"] = ["IVC-33000 DG GESTION DE INTERVENCION SOCIAL", .. BuzonesTecnica],
        ["RD-03/11-Análisis DGAyF"] = BuzonesDgayf,
        ["RD-04/11-Dgtal-Dictamen"] = BuzonesDictamen,
        ["RD-04a/11-Tecnica Rta Dictamen"] = ["IVC-1000 GERENCIA GENERAL"],
        ["RD-05/11-Firma AA Empresa"] = Redeterminaciones,
        // El legado tenía "RD-06/11Firma AA cd" (sin guion) y por eso esa etapa nunca marcaba
        // discrepancia; ESTADOS_REDET la llama "RD-06/11-Firma AA cd". Lo mismo para RO-06.
        ["RD-06/11-Firma AA cd"] = Redeterminaciones,
        ["RD-07/11-Aprobacion Anterior"] = Redeterminaciones,
        ["RD-08/11-Reser. Presup"] = BuzonesPresupuesto,
        ["RD-09/11-Dgtal-Dispo"] = BuzonesDispo,
        ["RD-10/11-Notificar a la empresa"] = Redeterminaciones,
        ["RD-11/11-Notificada"] = ["IVC-4010 DEPTO REDETERMINACIONES", "IVC-1860 DEPTO REGISTRO Y GUARDA DE DOCUMENTACION", "MGEYA-D.G.MESA DE ENTRADAS SALIDAS Y ARCHIVO"],
        ["Rectificacion"] = ["IVC-3160 DEPTO PROYECTOS"],
        ["RO-01/11-Creacion Expediente"] = ["IVC-2320 DEPTO ORGANIZACION Y CAPACITACION CONSORCIAL"],
        ["RO-02/11-Análisis Tecnica"] = BuzonesTecnica,
        ["RO-03/11-Análisis DGAyF"] = BuzonesDgayf,
        ["RO-04/11-Dgtal-Dictamen"] = ["IVC-5200 GO ASUNTOS LEGALES", "IVC-1400 DG TECNICO ADMINISTRATIVA Y LEGAL", "IVC-1000 GERENCIA GENERAL", "IVC-5120 DEPTO ASUNTOS JUDICIALES"],
        ["RO-05/11-Firma AA Empresa"] = ["HRR-HOSP.DE REHABILITACION MANUEL ROCCA"],
        ["RO-06/11-Firma AA cd"] = ["IVC-6000 DG CREDITOS Y ALQUILERES"],
        ["RO-07/11-Aprobacion Anterior"] = ["IVC-02 DESPACHO (AAG)"],
        ["RO-08/11-Reser. Presup"] = BuzonesPresupuesto,
        ["RO-09/11-Dgtal-Dispo"] = BuzonesDispo,
        ["RO-10/11-Notificar a la empresa"] = ["IVC-6230 DEPTO SOCIAL Y ADMINISTRATIVO"],
        ["RO-11/11-Notificada"] = ["IVC-9510 DEPTO TECNOLOGIA", "IVC-1860 DEPTO REGISTRO Y GUARDA DE DOCUMENTACION", "MGEYA-D.G.MESA DE ENTRADAS SALIDAS Y ARCHIVO"],
        ["RP-01/09-Subsanacion Empresa"] = Redeterminaciones,
        ["RP-02/09-Análisis Tecnica"] = BuzonesTecnica,
        ["RP-03/09-Análisis DGAyF"] = BuzonesDgayf,
        ["RP-04/09-Dgtal-Dictamen"] = BuzonesDictamen,
        ["RP-04a/09-Tecnica Rta Dictamen"] = ["IVC-1000 GERENCIA GENERAL"],
        ["RP-05/09-Aprobacion Anterior"] = ["IVC-8320 DEPTO REGULARIZACION DOMINIAL"],
        ["RP-06/09-Reser. Presup"] = BuzonesPresupuesto,
        ["RP-07/09-Dgtal-Dispo"] = BuzonesDispo,
        ["RP-08/09-Notificar a la empresa"] = ["IVC-ALS_MIGRADO_EX_UGIS ADMINISTRADOR LOCAL SUME"],
        ["RP-09/09-Notificada"] = ["IVC-1260 DEPTO GESTION DE ALQUILERES", "IVC-1860 DEPTO REGISTRO Y GUARDA DE DOCUMENTACION", "MGEYA-D.G.MESA DE ENTRADAS SALIDAS Y ARCHIVO"],
    };

    /// <summary>Etapas con correspondencia definida (para verificar contra ESTADOS_REDET).</summary>
    public static IEnumerable<string> EtapasConocidas => Correspondencias.Keys;

    /// <summary>
    /// True si el buzón de SADE no corresponde a la etapa. Sin etapa, sin buzón o con una etapa
    /// que no está en la tabla de correspondencias no hay discrepancia.
    /// </summary>
    public static bool IsMismatch(string? etapaNombre, string? buzonSade)
    {
        if (string.IsNullOrEmpty(etapaNombre) || string.IsNullOrEmpty(buzonSade))
            return false;
        if (!Correspondencias.TryGetValue(etapaNombre, out var permitidos))
            return false;
        // Hay discrepancia si NINGUNO de los buzones permitidos está contenido en el buzón del pase.
        return !permitidos.Any(b => buzonSade.Contains(b, StringComparison.Ordinal));
    }
}
