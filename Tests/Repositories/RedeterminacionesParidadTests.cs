using IVC.Application.Identidad;
using IVC.Application.Redeterminaciones;
using IVC.Data;
using IVC.Repositories.Implementations;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Repositories;

/// <summary>
/// Paridad del módulo de redeterminaciones contra una base IVC real (sólo lectura): los
/// cuatro servicios, con sus repositorios reales y un administrador, se comparan fila por
/// fila con las vistas del Power BI (vw_PBI_Redeterminaciones, vw_PBI_Reliquidaciones y
/// vw_PBI_Pagos de Database/002_vw_PowerBI_Redeterminaciones.sql de este repo), que implementan
/// las mismas reglas R1–R7 en SQL. Corre sólo con IVC_TEST_CONNECTION y exige que las
/// vistas existan en esa base.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class RedeterminacionesParidadTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private sealed class VistaRedet
    {
        public int RedeterminacionId { get; set; }
        public string? CodigoRedet { get; set; }
        public int EstadoGeneralId { get; set; }
        public decimal? MontoRedet { get; set; }
        public string? BuzonSade { get; set; }
        public DateTime? FechaSade { get; set; }
    }

    private sealed class VistaReliq
    {
        public int RedeterminacionId { get; set; }
        public string? CodigoRedet { get; set; }
        public int CertificadoId { get; set; }
        public DateTime? MesAprobacion { get; set; }
        public decimal? PorcentajeAplicado { get; set; }
        public decimal? MontoReliquidacion { get; set; }
        public string? ExpedienteReliq { get; set; }
    }

    private sealed class VistaPago
    {
        public string? Origen { get; set; }
        public int? CertificadoId { get; set; }
        public int? RedeterminacionId { get; set; }
        public DateTime? MesAprobacion { get; set; }
        public decimal? Monto { get; set; }
        public decimal? Sigaf { get; set; }
        public string? Estado { get; set; }
        public string? BuzonSade { get; set; }
    }

    private sealed class Armado
    {
        public Factory Factory { get; }
        public AutorizantesService Autorizantes { get; }
        public CertificadosService Certificados { get; }
        public LegitimosService Legitimos { get; }
        public RedeterminacionesService Redeterminaciones { get; }

        public Armado(string cs)
        {
            Factory = new Factory(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options);
            var admin = new UsuarioIvc(true, "test", "TEST", null, true, Array.Empty<int>(), Array.Empty<int>(), null, null, false);
            var usuarioActual = new Mock<IUsuarioActual>();
            usuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admin);
            var permisos = Mock.Of<IPermisosIvc>();
            var externos = new DatosExternosService(new DatosExternosRepository(Factory));
            var autorizantes = new AutorizanteRepository(Factory);
            var redets = new RedeterminacionRepository(Factory);
            var certs = new CertificadoRepository(Factory);
            Autorizantes = new AutorizantesService(autorizantes, redets, certs, new ObraRepository(Factory), externos, usuarioActual.Object, permisos, NullLogger<AutorizantesService>.Instance);
            Certificados = new CertificadosService(certs, autorizantes, redets, new ExpedienteReliqRepository(Factory), externos, usuarioActual.Object, permisos, NullLogger<CertificadosService>.Instance);
            Legitimos = new LegitimosService(new LegitimoRepository(Factory), new ObraRepository(Factory), externos, usuarioActual.Object, permisos, NullLogger<LegitimosService>.Instance);
            Redeterminaciones = new RedeterminacionesService(redets, autorizantes, externos, permisos, TimeProvider.System, NullLogger<RedeterminacionesService>.Instance);
        }
    }

    private static async Task<Armado?> ArmarAsync()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var armado = new Armado(cs);
        await using var db = armado.Factory.CreateDbContext();
        var vistas = await db.Database.SqlQuery<string>($"SELECT name AS Value FROM sys.views WHERE name IN ('vw_PBI_Redeterminaciones', 'vw_PBI_Reliquidaciones', 'vw_PBI_Pagos')").ToListAsync();
        if (vistas.Count != 3)
            Assert.Inconclusive("Faltan las vistas vw_PBI_* en la base: ejecutar Database/vw_PowerBI_Redeterminaciones.sql del legado.");
        return armado;
    }

    private static string ClaveMes(string? codigo, DateTime? mes) => $"{codigo}|{mes:yyyyMM}";

    [TestMethod]
    public async Task Autorizantes_LasFilasVirtuales_CoincidenConVwPbiRedeterminaciones()
    {
        var armado = await ArmarAsync();
        if (armado is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var filas = await armado.Autorizantes.GetAllAsync();
        await using var db = armado.Factory.CreateDbContext();
        var vista = await db.Database.SqlQuery<VistaRedet>($"SELECT RedeterminacionId, CodigoRedet, EstadoGeneralId, MontoRedet, BuzonSade, FechaSade FROM dbo.vw_PBI_Redeterminaciones").ToListAsync();
        var reales = await db.Autorizantes.AsNoTracking().CountAsync();

        var virtuales = filas.Where(f => f.EsRedeterminacion).ToDictionary(f => f.RedeterminacionId!.Value);
        Console.WriteLine($"Autorizantes reales: {filas.Count(f => !f.EsRedeterminacion)} (tabla: {reales}); virtuales: {virtuales.Count}; vista: {vista.Count}");
        Assert.AreEqual(reales, filas.Count(f => !f.EsRedeterminacion));
        Assert.AreEqual(vista.Count, virtuales.Count, "una fila virtual por redeterminación");

        var diferencias = new List<string>();
        foreach (var v in vista)
        {
            if (!virtuales.TryGetValue(v.RedeterminacionId, out var f)) { diferencias.Add($"{v.CodigoRedet}: falta en la app"); continue; }
            if (f.CodigoAutorizante != v.CodigoRedet) diferencias.Add($"{v.CodigoRedet}: código {f.CodigoAutorizante}");
            if (f.EstadoId != v.EstadoGeneralId) diferencias.Add($"{v.CodigoRedet}: estado {f.EstadoId} vs vista {v.EstadoGeneralId}");
            var montoApp = decimal.Round(f.MontoAutorizado ?? 0, 2);
            var montoVista = decimal.Round(v.MontoRedet ?? 0, 2);
            if (montoApp != montoVista) diferencias.Add($"{v.CodigoRedet}: MontoRedet {montoApp} vs vista {montoVista}");
            if ((f.BuzonSade ?? "") != (v.BuzonSade ?? "")) diferencias.Add($"{v.CodigoRedet}: buzón '{f.BuzonSade}' vs vista '{v.BuzonSade}'");
            if (f.FechaSade != v.FechaSade) diferencias.Add($"{v.CodigoRedet}: fecha SADE {f.FechaSade} vs vista {v.FechaSade}");
        }
        foreach (var d in diferencias) Console.WriteLine("  " + d);
        Assert.AreEqual(0, diferencias.Count, string.Join(" | ", diferencias.Take(10)));
    }

    [TestMethod]
    public async Task Certificados_ReliquidacionesYSigaf_CoincidenConVwPbiReliquidacionesYPagos()
    {
        var armado = await ArmarAsync();
        if (armado is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var filas = await armado.Certificados.GetAllAsync();
        await using var db = armado.Factory.CreateDbContext();
        var reliqs = await db.Database.SqlQuery<VistaReliq>($"SELECT RedeterminacionId, CodigoRedet, CertificadoId, MesAprobacion, PorcentajeAplicado, MontoReliquidacion, ExpedienteReliq FROM dbo.vw_PBI_Reliquidaciones").ToListAsync();
        var pagos = await db.Database.SqlQuery<VistaPago>($"SELECT Origen, CertificadoId, RedeterminacionId, MesAprobacion, Monto, Sigaf, Estado, BuzonSade FROM dbo.vw_PBI_Pagos").ToListAsync();
        var certificadosConAutorizante = await db.Certificados.AsNoTracking().CountAsync(c => db.Autorizantes.Any(a => a.CodigoAutorizante == c.CodigoAutorizante));

        var realesApp = filas.Where(f => !f.EsReliquidacion).ToDictionary(f => f.Id);
        var reliqApp = filas.Where(f => f.EsReliquidacion).GroupBy(f => ClaveMes(f.CodigoAutorizante, f.MesAprobacion)).ToDictionary(g => g.Key, g => g.ToList());
        Console.WriteLine($"Certificados reales: {realesApp.Count} (con autorizante en tabla: {certificadosConAutorizante}); reliquidaciones app: {filas.Count(f => f.EsReliquidacion)}; vista: {reliqs.Count}");
        Assert.AreEqual(certificadosConAutorizante, realesApp.Count, "el legado sólo listaba certificados con autorizante (INNER JOIN)");
        Assert.AreEqual(reliqs.Count, filas.Count(f => f.EsReliquidacion), "misma cantidad de reliquidaciones que la vista");

        var diferencias = new List<string>();
        // Reliquidaciones: por código de redeterminación y mes (clave de EXPEDIENTES_RELIQ), comparando monto, porcentaje y expediente.
        foreach (var grupo in reliqs.GroupBy(r => ClaveMes(r.CodigoRedet, r.MesAprobacion)))
        {
            if (!reliqApp.TryGetValue(grupo.Key, out var app)) { diferencias.Add($"reliq {grupo.Key}: falta en la app"); continue; }
            var montoVista = decimal.Round(grupo.Sum(r => r.MontoReliquidacion ?? 0), 2);
            var montoApp = decimal.Round(app.Sum(a => a.MontoTotal ?? 0), 2);
            // La vista calcula en decimal(28,12) y recorta a 4 decimales; .NET con 28 dígitos significativos: hasta un centavo de diferencia.
            if (Math.Abs(montoApp - montoVista) > 0.01m) diferencias.Add($"reliq {grupo.Key}: monto {montoApp} vs vista {montoVista}");
            var porcVista = decimal.Round(grupo.Sum(r => r.PorcentajeAplicado ?? 0), 4);
            var porcApp = decimal.Round(app.Sum(a => a.Porcentaje ?? 0), 4);
            if (porcApp != porcVista) diferencias.Add($"reliq {grupo.Key}: porcentaje {porcApp} vs vista {porcVista}");
            var expVista = grupo.First().ExpedienteReliq?.Trim() ?? "";
            if (app.Any(a => (a.ExpedientePago?.Trim() ?? "") != expVista)) diferencias.Add($"reliq {grupo.Key}: expediente '{app[0].ExpedientePago}' vs vista '{expVista}'");
        }
        // Pagos reales: SIGAF prorrateado, estado y buzón por id de certificado.
        foreach (var p in pagos.Where(p => p.Origen == "CERTIFICADO" && p.CertificadoId is not null))
        {
            if (!realesApp.TryGetValue(p.CertificadoId!.Value, out var f)) continue; // certificados sin autorizante no se listan
            var sigafApp = f.Sigaf is decimal s ? decimal.Round(s, 2) : (decimal?)null;
            var sigafVista = p.Sigaf is decimal sv ? decimal.Round(sv, 2) : (decimal?)null;
            if (sigafApp != sigafVista) diferencias.Add($"cert {p.CertificadoId}: SIGAF {sigafApp} vs vista {sigafVista}");
            if (f.Estado != p.Estado) diferencias.Add($"cert {p.CertificadoId}: estado {f.Estado} vs vista {p.Estado}");
            if ((f.BuzonSade ?? "") != (p.BuzonSade ?? "")) diferencias.Add($"cert {p.CertificadoId}: buzón '{f.BuzonSade}' vs vista '{p.BuzonSade}'");
        }
        // Pagos virtuales: SIGAF de las reliquidaciones, por redeterminación y mes.
        foreach (var grupo in pagos.Where(p => p.Origen == "RELIQUIDACION").GroupBy(p => ClaveMes(reliqs.First(r => r.RedeterminacionId == p.RedeterminacionId).CodigoRedet, p.MesAprobacion)))
        {
            if (!reliqApp.TryGetValue(grupo.Key, out var app)) continue;
            var sigafVista = grupo.Any(p => p.Sigaf is not null) ? decimal.Round(grupo.Sum(p => p.Sigaf ?? 0), 2) : (decimal?)null;
            var sigafApp = app.Any(a => a.Sigaf is not null) ? decimal.Round(app.Sum(a => a.Sigaf ?? 0), 2) : (decimal?)null;
            if (sigafApp != sigafVista) diferencias.Add($"reliq {grupo.Key}: SIGAF {sigafApp} vs vista {sigafVista}");
        }
        foreach (var d in diferencias.Take(40)) Console.WriteLine("  " + d);
        Console.WriteLine($"Con SIGAF: {filas.Count(f => f.Sigaf is not null)}; devengados: {filas.Count(f => f.Estado == "DEVENGADO")}; en trámite: {filas.Count(f => f.Estado == "EN TRAMITE")}; no iniciados: {filas.Count(f => f.Estado == "NO INICIADO")}");
        Assert.AreEqual(0, diferencias.Count, string.Join(" | ", diferencias.Take(10)));
    }

    [TestMethod]
    public async Task Legitimos_SigafRepartidoPorExpediente_YRedeterminaciones_SadeCoincideConLaVista()
    {
        var armado = await ArmarAsync();
        if (armado is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var legitimos = await armado.Legitimos.GetAllAsync();
        var redets = await armado.Redeterminaciones.GetAllAsync();
        await using var db = armado.Factory.CreateDbContext();
        Assert.AreEqual(await db.Legitimos.AsNoTracking().CountAsync(), legitimos.Count);
        Assert.AreEqual(await db.Redeterminaciones.AsNoTracking().CountAsync(), redets.Count);

        // El devengado del expediente se reparte entre los legítimos que lo comparten (PR #19 del legado);
        // R4: sin mezclar con certificados. Las claves de DEVENGADOS traen espacios finales y otra grafía.
        var devengados = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in await db.Devengados.AsNoTracking()
            .Where(d => d.EeFinanciera != null)
            .GroupBy(d => d.EeFinanciera!)
            .Select(g => new { Expediente = g.Key, Total = g.Sum(d => d.ImportePp) ?? 0m })
            .ToListAsync())
            devengados[d.Expediente.Trim()] = devengados.GetValueOrDefault(d.Expediente.Trim()) + d.Total;
        var montosPorExpediente = legitimos
            .Where(l => !string.IsNullOrWhiteSpace(l.Expediente))
            .GroupBy(l => l.Expediente!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<decimal>)g.Select(l => l.Certificado ?? 0m).ToList(), StringComparer.OrdinalIgnoreCase);
        var diferencias = new List<string>();
        foreach (var l in legitimos)
        {
            decimal? esperado = null;
            if (!string.IsNullOrWhiteSpace(l.Expediente) && devengados.TryGetValue(l.Expediente.Trim(), out var t))
                esperado = ReglasRedeterminaciones.ProrratearSigaf(t, l.Certificado ?? 0m, montosPorExpediente[l.Expediente.Trim()]);
            if (decimal.Round(l.Sigaf ?? -1, 2) != decimal.Round(esperado ?? -1, 2)) diferencias.Add($"legítimo {l.Id}: SIGAF {l.Sigaf} vs {esperado}");
            var estado = ReglasRedeterminaciones.DeterminarEstado(l.Expediente, l.Sigaf);
            if (l.Estado != estado) diferencias.Add($"legítimo {l.Id}: estado {l.Estado} vs {estado}");
        }
        Console.WriteLine($"Legítimos: {legitimos.Count}; con SIGAF: {legitimos.Count(l => l.Sigaf is not null)}; con SADE: {legitimos.Count(l => l.BuzonSade is not null)}; " +
                          $"expedientes compartidos: {montosPorExpediente.Count(g => g.Value.Count > 1)}");

        var vista = await db.Database.SqlQuery<VistaRedet>($"SELECT RedeterminacionId, CodigoRedet, EstadoGeneralId, MontoRedet, BuzonSade, FechaSade FROM dbo.vw_PBI_Redeterminaciones")
            .ToDictionaryAsync(v => v.RedeterminacionId);
        foreach (var r in redets)
        {
            if (!vista.TryGetValue(r.Id, out var v)) { diferencias.Add($"redet {r.Id}: falta en la vista"); continue; }
            if (r.CodigoRedet != v.CodigoRedet) diferencias.Add($"redet {r.Id}: código {r.CodigoRedet} vs {v.CodigoRedet}");
            if ((r.BuzonSade ?? "") != (v.BuzonSade ?? "")) diferencias.Add($"redet {r.Id}: buzón '{r.BuzonSade}' vs '{v.BuzonSade}'");
            if (r.FechaSade != v.FechaSade) diferencias.Add($"redet {r.Id}: fecha {r.FechaSade} vs {v.FechaSade}");
        }
        Console.WriteLine($"Redeterminaciones: {redets.Count}; con obra: {redets.Count(r => r.Obra is not null)}; con SADE: {redets.Count(r => r.BuzonSade is not null)}; " +
                          $"con días: {redets.Count(r => r.DiasEnBuzon is not null)}; discrepancias: {redets.Count(r => r.Discrepancia)}");
        foreach (var d in diferencias.Take(40)) Console.WriteLine("  " + d);
        Assert.AreEqual(0, diferencias.Count, string.Join(" | ", diferencias.Take(10)));
    }
}
