using IVC.Application.Catalogos.Dtos;
using IVC.Application.Common;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// Servicios de catálogos: validan, exigen unicidad, traducen a conflicto la fila que
/// otro usuario borró y a mensaje el borrado que la base rechaza. En Proyectos y
/// Movimientos, además, los textos y cálculos derivados que muestran las grillas.
/// </summary>
[TestClass]
public class CatalogosServicesTests
{
    [TestMethod]
    public async Task CatalogoSimple_Create_RecortaValidaYExigeNombreUnico()
    {
        var repo = new Mock<ICatalogoSimpleRepository>();
        repo.Setup(r => r.ExisteNombreAsync<Barrio>("Lugano", 0, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<Barrio>(), It.IsAny<CancellationToken>()))
            .Callback<Barrio, CancellationToken>((b, _) => b.Id = 7).Returns(Task.CompletedTask);
        var servicio = new CatalogoSimpleService<Barrio>(repo.Object, NullLogger<CatalogoSimpleService<Barrio>>.Instance);

        var creado = await servicio.CreateAsync(new CatalogoSimpleViewModel { Nombre = "  Lugano  " });

        Assert.AreEqual(7, creado.Id);
        Assert.AreEqual("Lugano", creado.Nombre);
        repo.Verify(r => r.AddAsync(It.Is<Barrio>(b => b.Nombre == "Lugano"), It.IsAny<CancellationToken>()), Times.Once);

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.CreateAsync(new CatalogoSimpleViewModel { Nombre = " " }));

        repo.Setup(r => r.ExisteNombreAsync<Barrio>("Soldati", 0, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var duplicado = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.CreateAsync(new CatalogoSimpleViewModel { Nombre = "Soldati" }));
        StringAssert.Contains(duplicado.Message, "Ya existe");
    }

    [TestMethod]
    public async Task CatalogoSimple_Update_DeFilaBorrada_EsConflicto_YDelete_EnUso_EsMensaje()
    {
        var repo = new Mock<ICatalogoSimpleRepository>();
        repo.Setup(r => r.GetByIdAsync<Empresa>(5, It.IsAny<CancellationToken>())).ReturnsAsync((Empresa?)null);
        repo.Setup(r => r.DeleteAsync<Empresa>(9, It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("FK"));
        var servicio = new CatalogoSimpleService<Empresa>(repo.Object, NullLogger<CatalogoSimpleService<Empresa>>.Instance);

        await Assert.ThrowsExceptionAsync<ConflictoDeConcurrenciaException>(
            () => servicio.UpdateAsync(new CatalogoSimpleViewModel { Id = 5, Nombre = "X" }));

        var enUso = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.DeleteAsync(9));
        StringAssert.Contains(enUso.Message, "en uso");
    }

    [TestMethod]
    public async Task LineasFF_Create_ResuelveLaLinea_YUpdate_EscribeUnaEntidadSinNavegaciones()
    {
        var linea = new LineaGestion { Id = 3, Nombre = "VIVIENDA", Tipo = "T", Grupo = "G", Reparticion = "R" };
        var lineas = new Mock<ILineaGestionRepository>();
        lineas.Setup(l => l.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(linea);
        lineas.Setup(l => l.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((LineaGestion?)null);

        var repo = new Mock<ILineaGestionFFRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<LineaGestionFF>(), It.IsAny<CancellationToken>()))
            .Callback<LineaGestionFF, CancellationToken>((f, _) => f.Id = 11).Returns(Task.CompletedTask);
        repo.Setup(r => r.GetByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LineaGestionFF { Id = 11, LineaGestionId = 3, LineaGestion = linea, Nombre = "FF", Fuente = "F" });
        LineaGestionFF? guardada = null;
        repo.Setup(r => r.UpdateAsync(It.IsAny<LineaGestionFF>(), It.IsAny<CancellationToken>()))
            .Callback<LineaGestionFF, CancellationToken>((f, _) => guardada = f).Returns(Task.CompletedTask);
        var servicio = new LineasGestionFFService(repo.Object, lineas.Object, NullLogger<LineasGestionFFService>.Instance);

        var creado = await servicio.CreateAsync(new LineaGestionFFViewModel { LineaGestionId = 3, Nombre = " FF ", Fuente = " " });
        Assert.AreEqual(11, creado.Id);
        Assert.AreEqual("VIVIENDA", creado.LineaGestionNombre);
        Assert.AreEqual("R", creado.Reparticion, "los datos de la línea vuelven con la fila");
        repo.Verify(r => r.AddAsync(It.Is<LineaGestionFF>(f => f.Nombre == "FF" && f.Fuente == null), It.IsAny<CancellationToken>()));

        var sinLinea = await Assert.ThrowsExceptionAsync<ArgumentException>(
            () => servicio.CreateAsync(new LineaGestionFFViewModel { LineaGestionId = 99, Nombre = "FF" }));
        StringAssert.Contains(sinLinea.Message, "ya no existe");

        await servicio.UpdateAsync(new LineaGestionFFViewModel { Id = 11, LineaGestionId = 3, Nombre = "FF 2", Fuente = "Tesoro" });
        Assert.IsNotNull(guardada);
        Assert.AreEqual("FF 2", guardada!.Nombre);
        Assert.IsNull(guardada.LineaGestion, "la navegación no viaja al Update: EF marcaría la línea como modificada");
    }

    private static Proyecto ProyectoDePrueba() => new()
    {
        Id = 100,
        ObraId = 10,
        Nombre = "PROY",
        SubProyecto = "SUB",
        LineaGestionId = 1,
        LineaGestion = new LineaGestion { Id = 1, Nombre = "VIVIENDA" },
        Autorizado2025 = 1000m,
        Autorizado2026 = 1500m,
        Obra = new Obra
        {
            Id = 10, Descripcion = "TORRE A", Numero = 12, Anio = 2024,
            Barrio = new Barrio { Nombre = "LUGANO" },
            Contrata = new Contrata { Nombre = "LP" },
            Area = new Area { Nombre = "OBRAS" },
            Empresa = new Empresa { Nombre = "CONSTRUCTORA SA" },
        },
    };

    [TestMethod]
    public async Task Proyectos_MapeaLosTextosDelLegado_YRechazaUnaObraQueYaTieneProyecto()
    {
        var repo = new Mock<IProyectoRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ProyectoDePrueba()]);
        repo.Setup(r => r.ExisteParaObraAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var lineas = new Mock<ILineaGestionRepository>();
        lineas.Setup(l => l.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new LineaGestion { Id = 1 });
        var servicio = new ProyectosService(repo.Object, lineas.Object, NullLogger<ProyectosService>.Instance);

        var fila = (await servicio.GetAllAsync()).Single();
        Assert.AreEqual("LP 12/2024", fila.Contrata);
        Assert.AreEqual("TORRE A - LUGANO", fila.Obra);
        Assert.AreEqual("OBRAS", fila.Area);
        Assert.AreEqual("CONSTRUCTORA SA", fila.Empresa);
        Assert.AreEqual("VIVIENDA", fila.LineaGestionNombre);

        var repetida = await Assert.ThrowsExceptionAsync<ArgumentException>(() => servicio.CreateAsync(
            new ProyectoViewModel { ObraId = 10, Nombre = "OTRO", LineaGestionId = 1, Autorizado2026 = 1m }));
        StringAssert.Contains(repetida.Message, "ya tiene un proyecto");
    }

    [TestMethod]
    public async Task Proyectos_Update_ConservaObraYAutorizado2025_YNoEnviaNavegaciones()
    {
        var repo = new Mock<IProyectoRepository>();
        repo.Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(ProyectoDePrueba());
        Proyecto? guardado = null;
        repo.Setup(r => r.UpdateAsync(It.IsAny<Proyecto>(), It.IsAny<CancellationToken>()))
            .Callback<Proyecto, CancellationToken>((p, _) => guardado = p).Returns(Task.CompletedTask);
        var lineas = new Mock<ILineaGestionRepository>();
        lineas.Setup(l => l.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new LineaGestion { Id = 2 });
        var servicio = new ProyectosService(repo.Object, lineas.Object, NullLogger<ProyectosService>.Instance);

        await servicio.UpdateAsync(new ProyectoViewModel
        {
            Id = 100, ObraId = 999, Nombre = " NUEVO ", SubProyecto = null, LineaGestionId = 2, Autorizado2025 = 5m, Autorizado2026 = 2000m,
        });

        Assert.IsNotNull(guardado);
        Assert.AreEqual(10, guardado!.ObraId, "la obra no se cambia desde la página");
        Assert.AreEqual(1000m, guardado.Autorizado2025, "el histórico se conserva");
        Assert.AreEqual("NUEVO", guardado.Nombre);
        Assert.AreEqual(string.Empty, guardado.SubProyecto, "SUBPROYECTO es NOT NULL");
        Assert.AreEqual(2, guardado.LineaGestionId);
        Assert.AreEqual(2000m, guardado.Autorizado2026);
        Assert.IsNull(guardado.Obra);
        Assert.IsNull(guardado.LineaGestion);
    }

    [TestMethod]
    public async Task Movimientos_AutorizadoNuevo_EsAutorizado2025MasLaSumaDeLaObra_YNullSinProyecto()
    {
        var conProyecto = new Obra
        {
            Id = 10, Descripcion = "TORRE A", Barrio = new Barrio { Nombre = "LUGANO" },
            Proyecto = new Proyecto { Nombre = "PROY", SubProyecto = "SUB", Autorizado2025 = 1000m, LineaGestion = new LineaGestion { Nombre = "VIVIENDA" } },
        };
        var sinProyecto = new Obra { Id = 20, Descripcion = "TORRE B" };
        var repo = new Mock<IMovimientoRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MovimientoGestion { Id = 1, ObraId = 10, Obra = conProyecto, Monto = 100m, Fecha = new DateTime(2026, 1, 1) },
            new MovimientoGestion { Id = 2, ObraId = 10, Obra = conProyecto, Monto = 50.5m, Fecha = new DateTime(2026, 2, 1) },
            new MovimientoGestion { Id = 3, ObraId = 20, Obra = sinProyecto, Monto = 7m, Fecha = new DateTime(2026, 3, 1) },
        ]);
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MovimientoGestion { Id = 1, ObraId = 10, Obra = conProyecto, Monto = 100m, Fecha = new DateTime(2026, 1, 1) });
        repo.Setup(r => r.SumarMontoPorObraAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(150.5m);
        var servicio = new MovimientosService(repo.Object, NullLogger<MovimientosService>.Instance);

        var filas = await servicio.GetAllAsync();
        Assert.AreEqual(1150.5m, filas[0].Autorizado2026, "1000 + (100 + 50,5): todos los movimientos de la obra");
        Assert.AreEqual(1150.5m, filas[1].Autorizado2026);
        Assert.IsNull(filas[2].Autorizado2026, "sin proyecto no hay base: como el LEFT JOIN del legado");
        Assert.AreEqual("TORRE A - LUGANO", filas[0].Obra);
        Assert.AreEqual("VIVIENDA", filas[0].Linea);
        Assert.AreEqual("TORRE B", filas[2].Obra, "sin barrio no se agrega el guion");

        var una = await servicio.GetByIdAsync(1);
        Assert.AreEqual(1150.5m, una!.Autorizado2026, "de a una fila la suma la trae el repositorio");
    }

    [TestMethod]
    public async Task Movimientos_Update_NoCambiaLaObra_YNormalizaLaFechaSinHora()
    {
        var repo = new Mock<IMovimientoRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MovimientoGestion { Id = 1, ObraId = 10, Obra = new Obra { Id = 10 }, Monto = 100m, Fecha = new DateTime(2026, 1, 1) });
        MovimientoGestion? guardado = null;
        repo.Setup(r => r.UpdateAsync(It.IsAny<MovimientoGestion>(), It.IsAny<CancellationToken>()))
            .Callback<MovimientoGestion, CancellationToken>((m, _) => guardado = m).Returns(Task.CompletedTask);
        var servicio = new MovimientosService(repo.Object, NullLogger<MovimientosService>.Instance);

        await servicio.UpdateAsync(new MovimientoViewModel { Id = 1, ObraId = 55, Monto = 200m, Fecha = new DateTime(2026, 5, 10, 14, 30, 0) });

        Assert.AreEqual(10, guardado!.ObraId);
        Assert.AreEqual(200m, guardado.Monto);
        Assert.AreEqual(new DateTime(2026, 5, 10), guardado.Fecha);
        Assert.IsNull(guardado.Obra);
    }
}
