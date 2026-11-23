using IVC.Application.Common;
using IVC.Application.Formulacion;
using IVC.Application.Formulacion.Dtos;
using IVC.Application.Identidad;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// El servicio de formulación: pivot por obra con los montos en la posición de su año,
/// visibilidad por área, PPI y techo sólo del administrador, y las escrituras con la formulación
/// abierta, la obra del usuario y la regla alta/edición del legado.
/// </summary>
[TestClass]
public class FormulacionServiceTests
{
    private static UsuarioIvc Usuario(bool admin, params int[] areas) => new(
        true, "u1", "ANA", null, admin, Array.Empty<int>(), areas, areas.Length > 0 ? areas[0] : null, null, false);

    private sealed class Armado
    {
        public Mock<IFormulacionRepository> Repo { get; } = new();
        public Mock<IObraRepository> Obras { get; } = new();
        public Mock<ILookupRepository> Lookups { get; } = new();
        public Mock<IUsuarioActual> UsuarioActual { get; } = new();
        public Mock<IPermisosIvc> Permisos { get; } = new();

        public Armado(UsuarioIvc usuario, bool formulacionAbierta = true, bool obraConFormulacion = true)
        {
            UsuarioActual.Setup(u => u.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
            Permisos.Setup(p => p.PuedeModificarFormulacionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(formulacionAbierta);
            Repo.Setup(r => r.GetDelCicloAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(FilasDePrueba());
            Repo.Setup(r => r.GetDeObraAsync(10, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(obraConFormulacion ? FilasDePrueba().Where(f => f.ObraId == 10).ToList() : []);
            Repo.Setup(r => r.GetDeObraAsync(It.Is<int>(id => id != 10), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            Repo.Setup(r => r.EliminarDeObraAsync(10, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Obras.Setup(o => o.GetByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new Obra { Id = 10, AreaId = 26, EmpresaId = 2 });
            Obras.Setup(o => o.GetByIdAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(new Obra { Id = 20, AreaId = 40, EmpresaId = 2 });
            Lookups.Setup(l => l.GetObrasSinFormulacionAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new OpcionLookup(20, "OBRA 20")]);
        }

        public FormulacionService Servicio(int anioBase = 2027) => new(
            Repo.Object, Obras.Object, Lookups.Object, UsuarioActual.Object, Permisos.Object,
            Options.Create(new FormulacionOptions { AnioBase = anioBase }), NullLogger<FormulacionService>.Instance);
    }

    private static Obra ObraDePrueba() => new()
    {
        Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024, AreaId = 26, Area = new Area { Id = 26, Nombre = "DGDH" },
        EmpresaId = 2, Empresa = new Empresa { Id = 2, Nombre = "CONSTRUCTORA" }, Contrata = new Contrata { Id = 3, Nombre = "LP" },
        Barrio = new Barrio { Id = 4, Nombre = "LUGANO" },
        Proyecto = new Proyecto { Nombre = "PROY", LineaGestion = new LineaGestion { Nombre = "VIVIENDA" } },
    };

    /// <summary>Obra 10 con montos en 2027 y 2029 (2028 vacío); obra 30 sólo en 2028.</summary>
    private static List<Formulacion> FilasDePrueba()
    {
        var obra = ObraDePrueba();
        return
        [
            new Formulacion
            {
                Id = 1, ObraId = 10, Obra = obra, FechaPeriodo = new DateTime(2027, 1, 1), Monto = 100m, Ppi = 7, Techos = 900m,
                MesBase = new DateTime(2026, 6, 1), UnidadMedidaId = 1, UnidadMedida = new UnidadMedida { Id = 1, Nombre = "VIVIENDAS" },
                ValorMedida = 24m, PrioridadId = 2, Prioridad = new Prioridad { Id = 2, Nombre = "ALTA" }, BreveDescripcion = "DESC", Observaciones = "OBS",
                FechaInicio = new DateTime(2027, 3, 1), FechaFin = new DateTime(2028, 3, 1),
            },
            new Formulacion
            {
                Id = 2, ObraId = 10, Obra = obra, FechaPeriodo = new DateTime(2029, 1, 1), Monto = 300m, Ppi = 7, Techos = 900m,
                UnidadMedidaId = 1, ValorMedida = 24m, PrioridadId = 2, BreveDescripcion = "DESC", Observaciones = "OBS",
            },
            new Formulacion
            {
                Id = 3, ObraId = 30, Obra = new Obra { Id = 30, Descripcion = "TORRE B", AreaId = 26, EmpresaId = 2 },
                FechaPeriodo = new DateTime(2028, 1, 1), Monto = 50m, UnidadMedidaId = 1, ValorMedida = 1m, PrioridadId = 2,
            },
        ];
    }

    [TestMethod]
    public async Task GetAll_PivoteaPorObra_ConLosMontosEnLaPosicionDeSuAnio()
    {
        var armado = new Armado(Usuario(admin: true));
        var filas = await armado.Servicio().GetAllAsync();
        armado.Repo.Verify(r => r.GetDelCicloAsync(
            It.Is<IReadOnlyCollection<int>>(a => a.SequenceEqual(new[] { 2027, 2028, 2029 })), null, It.IsAny<CancellationToken>()), Times.Once,
            "el administrador no filtra por área");

        Assert.AreEqual(2, filas.Count, "una fila por obra, no por año");
        var torreA = filas.Single(f => f.ObraId == 10);
        Assert.AreEqual("TORRE A", torreA.Obra);
        Assert.AreEqual("DGDH", torreA.Area);
        Assert.AreEqual("LP 12/2024", torreA.Contrata);
        Assert.AreEqual("VIVIENDA", torreA.LineaGestion);
        Assert.AreEqual(100m, torreA.MontoAnio1);
        Assert.IsNull(torreA.MontoAnio2, "2028 no tiene fila");
        Assert.AreEqual(300m, torreA.MontoAnio3);
        Assert.AreEqual(400m, torreA.TotalMonto);
        Assert.AreEqual(7, torreA.Ppi);
        Assert.AreEqual("VIVIENDAS", torreA.UnidadMedida);
        Assert.AreEqual("ALTA", torreA.Prioridad);
        Assert.AreEqual(new DateTime(2026, 6, 1), torreA.MesBase, "los compartidos salen de la fila más antigua");

        var torreB = filas.Single(f => f.ObraId == 30);
        Assert.IsNull(torreB.MontoAnio1);
        Assert.AreEqual(50m, torreB.MontoAnio2);
    }

    [TestMethod]
    public async Task GetAll_UsuarioNormal_FiltraPorSusAreas_YSinAreasNoConsulta()
    {
        var normal = new Armado(Usuario(admin: false, 26, 40));
        await normal.Servicio().GetAllAsync();
        normal.Repo.Verify(r => r.GetDelCicloAsync(It.IsAny<IReadOnlyCollection<int>>(),
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 26, 40 })), It.IsAny<CancellationToken>()), Times.Once);

        var sinAreas = new Armado(Usuario(admin: false));
        Assert.AreEqual(0, (await sinAreas.Servicio().GetAllAsync()).Count);
        sinAreas.Repo.Verify(r => r.GetDelCicloAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.AreEqual(0, (await sinAreas.Servicio().GetObrasDisponiblesAsync()).Count, "tampoco ofrece obras para el alta");
    }

    [TestMethod]
    public async Task Ciclo_SaleDeLaConfiguracion()
    {
        var armado = new Armado(Usuario(admin: true));
        CollectionAssert.AreEqual(new[] { 2030, 2031, 2032 }, armado.Servicio(anioBase: 2030).Ciclo.Anios);
        var filas = await armado.Servicio(anioBase: 2029).GetAllAsync();
        Assert.AreEqual(300m, filas.Single(f => f.ObraId == 10).MontoAnio1, "con base 2029 el monto de 2029 pasa a la primera posición");
    }

    private static FormulacionViewModel VmValido(int obraId) => new()
    {
        ObraId = obraId, MontoAnio1 = 10m, MontoAnio3 = 20m, UnidadMedidaId = 1, ValorMedida = 2m, PrioridadId = 2, Ppi = 99,
        BreveDescripcion = "  nueva  ", Observaciones = "   ",
    };

    [TestMethod]
    public async Task Guardar_ExigeFormulacionAbierta_ObraDelArea_YObraSinFormulacionEnElAlta()
    {
        var cerrada = new Armado(Usuario(admin: true), formulacionAbierta: false);
        var ex = await Assert.ThrowsExceptionAsync<ArgumentException>(() => cerrada.Servicio().GuardarAsync(VmValido(20), esAlta: true));
        StringAssert.Contains(ex.Message, "formulación está cerrada");

        var otraArea = new Armado(Usuario(admin: false, 26));
        var ex2 = await Assert.ThrowsExceptionAsync<ArgumentException>(() => otraArea.Servicio().GuardarAsync(VmValido(20), esAlta: true));
        StringAssert.Contains(ex2.Message, "propia área");

        var yaFormulada = new Armado(Usuario(admin: true));
        var ex3 = await Assert.ThrowsExceptionAsync<ArgumentException>(() => yaFormulada.Servicio().GuardarAsync(VmValido(10), esAlta: true));
        StringAssert.Contains(ex3.Message, "ya tiene formulación");
        yaFormulada.Repo.Verify(r => r.GuardarPivotAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<Formulacion>(), It.IsAny<IReadOnlyList<decimal?>>(), It.IsAny<CancellationToken>()), Times.Never);

        var borrada = new Armado(Usuario(admin: true), obraConFormulacion: false);
        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => borrada.Servicio().GuardarAsync(VmValido(10), esAlta: false));

        var invalido = VmValido(20);
        invalido.MontoAnio1 = null; invalido.MontoAnio3 = null;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => new Armado(Usuario(admin: true)).Servicio().GuardarAsync(invalido, esAlta: true));
    }

    [TestMethod]
    public async Task Guardar_ReplicaLosCompartidos_YElPpiYElTechoSoloLosCargaElAdmin()
    {
        var admin = new Armado(Usuario(admin: true));
        Formulacion? compartido = null; IReadOnlyList<decimal?>? montos = null;
        admin.Repo.Setup(r => r.GuardarPivotAsync(10, It.IsAny<IReadOnlyList<int>>(), It.IsAny<Formulacion>(), It.IsAny<IReadOnlyList<decimal?>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<int>, Formulacion, IReadOnlyList<decimal?>, CancellationToken>((_, _, c, m, _) => { compartido = c; montos = m; })
            .Returns(Task.CompletedTask);

        var vmAdmin = VmValido(10);
        vmAdmin.Techos = 1234m;
        var releida = await admin.Servicio().GuardarAsync(vmAdmin, esAlta: false);
        Assert.IsNotNull(compartido);
        Assert.AreEqual(99, compartido.Ppi, "el administrador carga el PPI");
        Assert.AreEqual(1234m, compartido.Techos, "y el techo");
        Assert.AreEqual("nueva", compartido.BreveDescripcion, "recortada");
        Assert.IsNull(compartido.Observaciones, "en blanco se guarda null");
        CollectionAssert.AreEqual(new decimal?[] { 10m, null, 20m }, montos!.ToArray(), "un monto por año del ciclo, en orden");
        Assert.AreEqual("TORRE A", releida.Obra, "vuelve releída del repositorio");

        var normal = new Armado(Usuario(admin: false, 26));
        Formulacion? compartidoNormal = null;
        normal.Repo.Setup(r => r.GuardarPivotAsync(10, It.IsAny<IReadOnlyList<int>>(), It.IsAny<Formulacion>(), It.IsAny<IReadOnlyList<decimal?>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<int>, Formulacion, IReadOnlyList<decimal?>, CancellationToken>((_, _, c, _, _) => compartidoNormal = c)
            .Returns(Task.CompletedTask);
        var vmNormal = VmValido(10);
        vmNormal.Ppi = -1; vmNormal.Techos = -5m; // valores que no ve: no lo traban ni se persisten
        await normal.Servicio().GuardarAsync(vmNormal, esAlta: false);
        Assert.AreEqual(7, compartidoNormal!.Ppi, "el usuario normal conserva el PPI que ya estaba");
        Assert.AreEqual(900m, compartidoNormal.Techos, "y el techo que ya estaba");

        var alta = new Armado(Usuario(admin: false, 40));
        Formulacion? compartidoAlta = null;
        alta.Repo.Setup(r => r.GuardarPivotAsync(20, It.IsAny<IReadOnlyList<int>>(), It.IsAny<Formulacion>(), It.IsAny<IReadOnlyList<decimal?>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<int>, Formulacion, IReadOnlyList<decimal?>, CancellationToken>((_, _, c, _, _) => compartidoAlta = c)
            .Returns(Task.CompletedTask);
        alta.Repo.Setup(r => r.GetDeObraAsync(20, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => compartidoAlta is null
                ? new List<Formulacion>()
                : new List<Formulacion> { new() { ObraId = 20, Obra = new Obra { Id = 20, Descripcion = "OBRA 20" }, FechaPeriodo = new DateTime(2027, 1, 1) } });
        var vmAlta = VmValido(20);
        vmAlta.Techos = 50m;
        await alta.Servicio().GuardarAsync(vmAlta, esAlta: true);
        Assert.IsNull(compartidoAlta!.Ppi, "en el alta del usuario normal el PPI queda vacío");
        Assert.IsNull(compartidoAlta.Techos, "y el techo también, como el panel oculto del legado");
    }

    [TestMethod]
    public async Task Eliminar_ExigeFormulacionAbierta_YAvisaSiYaNoExiste()
    {
        var cerrada = new Armado(Usuario(admin: true), formulacionAbierta: false);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => cerrada.Servicio().EliminarAsync(10));

        var admin = new Armado(Usuario(admin: true));
        await admin.Servicio().EliminarAsync(10);
        admin.Repo.Verify(r => r.EliminarDeObraAsync(10, It.Is<IReadOnlyCollection<int>>(a => a.Count == 3), It.IsAny<CancellationToken>()), Times.Once);

        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(() => admin.Servicio().EliminarAsync(20));
    }

    [TestMethod]
    public async Task ObrasDisponibles_PideLasDelUsuarioSinFormulacionEnElCiclo()
    {
        var normal = new Armado(Usuario(admin: false, 26));
        var obras = await normal.Servicio().GetObrasDisponiblesAsync();
        Assert.AreEqual("OBRA 20", obras.Single().Nombre);
        normal.Lookups.Verify(l => l.GetObrasSinFormulacionAsync(
            It.Is<IReadOnlyCollection<int>>(a => a.SequenceEqual(new[] { 2027, 2028, 2029 })),
            It.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.SequenceEqual(new[] { 26 })), It.IsAny<CancellationToken>()), Times.Once);
    }
}
