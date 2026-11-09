using IVC.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Data;

/// <summary>
/// Lee una fila de cada tabla mapeada contra una base IVC real (sólo lectura). Verifica que
/// nombres de tabla y columna, tipos y nulabilidad del mapeo coincidan con el esquema: un
/// error acá es una columna inexistente, un NULL en una propiedad no nullable o un tipo
/// incompatible. Corre sólo con la variable de entorno IVC_TEST_CONNECTION (apuntar a
/// IVC_TEST, nunca a producción); sin ella queda Inconclusive y no rompe el CI.
/// </summary>
[TestClass]
[TestCategory("Integracion")]
public class IvcDbContextIntegracionTests
{
    private const string Variable = "IVC_TEST_CONNECTION";

    private static IvcDbContext? Crear()
    {
        var cs = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var options = new DbContextOptionsBuilder<IvcDbContext>().UseSqlServer(cs).Options;
        return new IvcDbContext(options);
    }

    [TestMethod]
    public async Task CadaTablaMapeada_SePuedeLeer()
    {
        await using var db = Crear();
        if (db is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        var fallas = new List<string>();
        async Task Leer<T>(IQueryable<T> q, string nombre) where T : class
        {
            try { _ = await q.AsNoTracking().Take(5).ToListAsync(); }
            catch (Exception ex) { fallas.Add($"{nombre}: {ex.GetBaseException().Message}"); }
        }

        await Leer(db.Obras, nameof(db.Obras));
        await Leer(db.Proyectos, nameof(db.Proyectos));
        await Leer(db.MovimientosGestion, nameof(db.MovimientosGestion));
        await Leer(db.Formulaciones, nameof(db.Formulaciones));
        await Leer(db.Legitimos, nameof(db.Legitimos));
        await Leer(db.Autorizantes, nameof(db.Autorizantes));
        await Leer(db.Certificados, nameof(db.Certificados));
        await Leer(db.Redeterminaciones, nameof(db.Redeterminaciones));
        await Leer(db.ExpedientesReliq, nameof(db.ExpedientesReliq));
        await Leer(db.Empresas, nameof(db.Empresas));
        await Leer(db.Contratas, nameof(db.Contratas));
        await Leer(db.Barrios, nameof(db.Barrios));
        await Leer(db.Areas, nameof(db.Areas));
        await Leer(db.Conceptos, nameof(db.Conceptos));
        await Leer(db.EstadosAutorizante, nameof(db.EstadosAutorizante));
        await Leer(db.EstadosRedet, nameof(db.EstadosRedet));
        await Leer(db.TiposPago, nameof(db.TiposPago));
        await Leer(db.Prioridades, nameof(db.Prioridades));
        await Leer(db.UnidadesMedida, nameof(db.UnidadesMedida));
        await Leer(db.LineasGestion, nameof(db.LineasGestion));
        await Leer(db.LineasGestionFF, nameof(db.LineasGestionFF));
        await Leer(db.AbmPlani, nameof(db.AbmPlani));
        await Leer(db.UsuariosVinculados, nameof(db.UsuariosVinculados));
        await Leer(db.Usuarios, nameof(db.Usuarios));
        await Leer(db.PasesSade, nameof(db.PasesSade));
        await Leer(db.Gedos, nameof(db.Gedos));
        await Leer(db.Devengados, nameof(db.Devengados));

        Assert.AreEqual(0, fallas.Count, string.Join(Environment.NewLine, fallas));
    }

    [TestMethod]
    public async Task LasNavegacionesYLaColumnaCalculada_SeMaterializan()
    {
        await using var db = Crear();
        if (db is null) { Assert.Inconclusive($"Sin {Variable}: test de integración omitido."); return; }

        // Obra con sus catálogos y su proyecto (0..1).
        var obra = await db.Obras.AsNoTracking()
            .Include(o => o.Empresa).Include(o => o.Area).Include(o => o.Barrio).Include(o => o.Contrata)
            .Include(o => o.Proyecto).ThenInclude(p => p!.LineaGestion)
            .OrderBy(o => o.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(obra);
        Assert.IsNotNull(obra.Empresa);
        Assert.IsNotNull(obra.Area);

        // Autorizante con obra, concepto y estado; redeterminación con etapa y código calculado.
        var autorizante = await db.Autorizantes.AsNoTracking()
            .Include(a => a.Obra).Include(a => a.Concepto).Include(a => a.Estado)
            .OrderBy(a => a.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(autorizante);

        var redet = await db.Redeterminaciones.AsNoTracking()
            .Include(r => r.Etapa).Include(r => r.Usuario)
            .Where(r => r.Nro != null && r.CodigoAutorizante != null)
            .OrderBy(r => r.Id).FirstOrDefaultAsync();
        Assert.IsNotNull(redet);
        Assert.AreEqual($"{redet.CodigoAutorizante}-R{redet.Nro}", redet.CodigoRedet,
            "Codigo_Redet calculado en la base no coincide con CODIGO_AUTORIZANTE-RNro.");

        // Consulta típica sobre una bajada externa sin clave, con parámetro varchar.
        var expediente = await db.PasesSade.AsNoTracking()
            .Where(p => p.Expediente != null).Select(p => p.Expediente!).FirstOrDefaultAsync();
        if (expediente is not null)
        {
            var pases = await db.PasesSade.AsNoTracking().Where(p => p.Expediente == expediente).ToListAsync();
            Assert.IsTrue(pases.Count >= 1);
        }
    }
}
