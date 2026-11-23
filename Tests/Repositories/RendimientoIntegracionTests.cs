using System.Data.Common;
using System.Diagnostics;
using IVC.Application.Formulacion;
using IVC.Application.Identidad;
using IVC.Data;
using IVC.Repositories.Implementations;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Repositories;

/// <summary>
/// Rendimiento de la carga completa de cada grilla contra una base IVC real (sólo lectura):
/// cuenta las consultas SQL que ejecuta cada servicio y mide el tiempo. La cantidad de
/// consultas tiene que ser constante (independiente del volumen): un N+1 se vería acá antes
/// que en producción, donde hay 8.750 reliquidaciones y 5.000 pagos. El tiempo se informa,
/// no se exige: depende de la red y del servidor. Corre sólo con IVC_TEST_CONNECTION.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class RendimientoIntegracionTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    /// <summary>
    /// Consultas fijas que puede ejecutar la carga completa de una grilla (una por tabla
    /// involucrada más los agregados). Las de redeterminaciones leen además los datos
    /// externos (SADE, SIGAF, ACDIR) en lotes de 800 expedientes: el tope suma un lote por
    /// fuente cada 800 filas, así que crece con los lotes y no con las filas (un N+1 daría
    /// tantas consultas como filas y falla igual).
    /// </summary>
    private const int ConsultasFijas = 6;
    private const int FuentesExternas = 3;
    private const int TamanioLote = DatosExternosRepository.TamanioLote;

    private static int MaxConsultas(int filas) => ConsultasFijas + FuentesExternas * Math.Max(1, (filas + TamanioLote - 1) / TamanioLote);

    private sealed class ContadorDeConsultas : DbCommandInterceptor
    {
        public int Consultas { get; private set; }
        public TimeSpan MasLenta { get; private set; }
        public TimeSpan TotalSql { get; private set; }
        public string? TextoMasLenta { get; private set; }
        public void Reiniciar() { Consultas = 0; MasLenta = TimeSpan.Zero; TotalSql = TimeSpan.Zero; TextoMasLenta = null; }

        private void Registrar(DbCommand command, CommandExecutedEventData eventData)
        {
            TotalSql += eventData.Duration;
            if (eventData.Duration <= MasLenta) return;
            MasLenta = eventData.Duration;
            var texto = command.CommandText.ReplaceLineEndings(" ");
            TextoMasLenta = texto.Length > 110 ? texto[..110] + "…" : texto;
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Registrar(command, eventData);
            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Registrar(command, eventData);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        {
            Registrar(command, eventData);
            return base.ScalarExecuted(command, eventData, result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        {
            Registrar(command, eventData);
            return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Consultas++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Consultas++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Consultas++;
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Consultas++;
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class Factory(DbContextOptions<IvcDbContext> options) : IDbContextFactory<IvcDbContext>
    {
        public IvcDbContext CreateDbContext() => new(options);
    }

    private sealed class Armado
    {
        public ContadorDeConsultas Contador { get; } = new();
        public Factory Factory { get; }
        public Mock<IUsuarioActual> UsuarioActual { get; } = new();
        public IPermisosIvc Permisos { get; } = Mock.Of<IPermisosIvc>();

        public Armado(string cs)
        {
            Factory = new Factory(new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).AddInterceptors(Contador).Options);
            var admin = new UsuarioIvc(true, "test", "TEST", null, true, Array.Empty<int>(), Array.Empty<int>(), null, null, false);
            UsuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        }
    }

    private sealed record Medicion(int Filas, int Consultas, long Ms, long MsSql, long MsMasLenta, string? MasLenta);

    private static async Task<Medicion> MedirAsync<T>(Armado armado, Func<Task<IReadOnlyList<T>>> cargar)
    {
        // Primera pasada para pagar la compilación del modelo y la apertura de la conexión.
        await cargar();
        armado.Contador.Reiniciar();
        var reloj = Stopwatch.StartNew();
        var filas = await cargar();
        reloj.Stop();
        return new(filas.Count, armado.Contador.Consultas, reloj.ElapsedMilliseconds, (long)armado.Contador.TotalSql.TotalMilliseconds,
            (long)armado.Contador.MasLenta.TotalMilliseconds, armado.Contador.TextoMasLenta);
    }

    [TestMethod]
    public async Task CargaCompletaDeCadaGrilla_UsaUnNumeroConstanteDeConsultas()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }
        var a = new Armado(cs);
        var f = a.Factory;
        var externos = new DatosExternosService(new DatosExternosRepository(f));
        var autorizantes = new AutorizanteRepository(f);
        var redets = new RedeterminacionRepository(f);
        var certs = new CertificadoRepository(f);
        var lookups = new LookupRepository(f);
        var opciones = Options.Create(new FormulacionOptions { AnioBase = FormulacionOptions.AnioBasePorDefecto });

        var grillas = new (string Nombre, Func<Task<Medicion>> Medir)[]
        {
            ("Obras", () => MedirAsync(a, () => new ObrasService(new ObraRepository(f), lookups, a.UsuarioActual.Object, a.Permisos, NullLogger<ObrasService>.Instance).GetAllAsync())),
            ("Autorizantes", () => MedirAsync(a, () => new AutorizantesService(autorizantes, redets, certs, new ObraRepository(f), externos, a.UsuarioActual.Object, a.Permisos, NullLogger<AutorizantesService>.Instance).GetAllAsync())),
            ("Certificados", () => MedirAsync(a, () => new CertificadosService(certs, autorizantes, redets, new ExpedienteReliqRepository(f), externos, a.UsuarioActual.Object, a.Permisos, NullLogger<CertificadosService>.Instance).GetAllAsync())),
            ("Legítimos", () => MedirAsync(a, () => new LegitimosService(new LegitimoRepository(f), new ObraRepository(f), externos, a.UsuarioActual.Object, a.Permisos, NullLogger<LegitimosService>.Instance).GetAllAsync())),
            ("Redeterminaciones", () => MedirAsync(a, () => new RedeterminacionesService(redets, autorizantes, externos, a.Permisos, TimeProvider.System, NullLogger<RedeterminacionesService>.Instance).GetAllAsync())),
            ("Formulación", () => MedirAsync(a, () => new FormulacionService(new FormulacionRepository(f), new ObraRepository(f), lookups, a.UsuarioActual.Object, a.Permisos, opciones, NullLogger<FormulacionService>.Instance).GetAllAsync())),
            ("Proyectos", () => MedirAsync(a, () => new ProyectosService(new ProyectoRepository(f), new LineaGestionRepository(f), NullLogger<ProyectosService>.Instance).GetAllAsync())),
            ("Movimientos", () => MedirAsync(a, () => new MovimientosService(new MovimientoRepository(f), NullLogger<MovimientosService>.Instance).GetAllAsync())),
        };

        var excesos = new List<string>();
        foreach (var (nombre, medir) in grillas)
        {
            var m = await medir();
            Console.WriteLine($"{nombre,-18} filas {m.Filas,6}  consultas {m.Consultas,3}  {m.Ms,6} ms (SQL {m.MsSql,5} ms; más lenta {m.MsMasLenta,4} ms: {m.MasLenta})");
            if (m.Consultas > MaxConsultas(m.Filas)) excesos.Add($"{nombre}: {m.Consultas} consultas para {m.Filas} filas (tope {MaxConsultas(m.Filas)})");
        }
        Assert.AreEqual(0, excesos.Count, "la cantidad de consultas no puede crecer con las filas (sin N+1):\n" + string.Join("\n", excesos));
    }
}
