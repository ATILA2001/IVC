using System.Security.Claims;
using IVC.Data.Entities;
using IVC.Repositories.Abstractions;
using IVC.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// Construcción del usuario IVC desde los claims de Auth.Web: roles de administrador,
/// áreas IVC resueltas por AuthAreaId, alta y sincronización en UsuariosVinculados.
/// </summary>
[TestClass]
public class UsuarioIvcFactoryTests
{
    private static ClaimsPrincipal Principal(string nombre, string[]? roles = null, int[]? areas = null, string id = "42", string? version = "7")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Name, nombre),
            new(ClaimTypes.Email, $"{nombre.ToLowerInvariant()}@ivc.test"),
        };
        claims.AddRange((roles ?? []).Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange((areas ?? []).Select(a => new Claim("area", a.ToString())));
        if (version is not null) claims.Add(new Claim("perms_version", version));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Identity.Application"));
    }

    private static (Mock<IAreaRepository> Areas, Mock<IUsuarioVinculadoRepository> Vinculados, UsuarioIvcFactory Factory) Armar()
    {
        var areas = new Mock<IAreaRepository>();
        areas.Setup(a => a.GetByAuthAreaIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Area>());
        var vinculados = new Mock<IUsuarioVinculadoRepository>();
        vinculados.Setup(v => v.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, string nombre, CancellationToken _) => new UsuarioVinculado { AuthUserId = id, Nombre = nombre });
        return (areas, vinculados, new UsuarioIvcFactory(areas.Object, vinculados.Object, NullLogger<UsuarioIvcFactory>.Instance));
    }

    [TestMethod]
    public async Task SinPrincipalAutenticado_DevuelveAnonimo_YNoTocaLaBase()
    {
        var (areas, vinculados, factory) = Armar();

        var usuario = await factory.ConstruirAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.AreSame(IVC.Application.Identidad.UsuarioIvc.Anonimo, usuario);
        areas.VerifyNoOtherCalls();
        vinculados.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RolAdmin_MarcaAdministrador()
    {
        var (_, _, factory) = Armar();
        var usuario = await factory.ConstruirAsync(Principal("Ana", roles: ["Admin"]));

        Assert.IsTrue(usuario.Autenticado);
        Assert.IsTrue(usuario.EsAdmin, "el rol Admin debe marcar al usuario como administrador");
        Assert.AreEqual("Ana", usuario.Nombre);
        Assert.AreEqual("42", usuario.AuthUserId);
    }

    [TestMethod]
    public async Task LasAreasDeAuthWeb_SeResuelvenAAreasIvc_YLaPrimeraEsLaPrincipal()
    {
        var (areas, _, factory) = Armar();
        areas.Setup(a => a.GetByAuthAreaIdsAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 36 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Area>
            {
                new() { Id = 18, Nombre = "SECRETARIA A", AuthAreaId = 36 },
                new() { Id = 19, Nombre = "SECRETARIA B", AuthAreaId = 36 },
                new() { Id = 20, Nombre = "SECRETARIA C", AuthAreaId = 36 },
            });

        var usuario = await factory.ConstruirAsync(Principal("Ana", areas: [36]));

        CollectionAssert.AreEqual(new[] { 36 }, usuario.AuthAreaIds.ToArray());
        CollectionAssert.AreEqual(new[] { 18, 19, 20 }, usuario.IvcAreaIds.ToArray());
        Assert.AreEqual(18, usuario.AreaPrincipalId);
        Assert.AreEqual("SECRETARIA A", usuario.AreaPrincipalNombre);
    }

    [TestMethod]
    public async Task PrimerIngreso_DaDeAltaEnUsuariosVinculados_YTomaElOverride()
    {
        var (_, vinculados, factory) = Armar();
        vinculados.Setup(v => v.GetOrCreateAsync("42", "Ana", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsuarioVinculado { AuthUserId = "42", Nombre = "Ana", PlanificacionAbierta = true });

        var usuario = await factory.ConstruirAsync(Principal("Ana"));

        Assert.IsTrue(usuario.PlanificacionAbiertaOverride);
        vinculados.Verify(v => v.GetOrCreateAsync("42", "Ana", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SinNombre_UsaElCorreo()
    {
        var (_, vinculados, factory) = Armar();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "9"), new(ClaimTypes.Email, "bruno@ivc.test") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Identity.Application"));

        var usuario = await factory.ConstruirAsync(principal);

        Assert.AreEqual("bruno@ivc.test", usuario.Nombre);
        vinculados.Verify(v => v.GetOrCreateAsync("9", "bruno@ivc.test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SiLaBaseFalla_NoSeAdivinaElUsuario()
    {
        var (areas, _, factory) = Armar();
        areas.Setup(a => a.GetByAuthAreaIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sql"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => factory.ConstruirAsync(Principal("Ana", areas: [36])));
    }

    [TestMethod]
    public void ClaveDeIdentidad_CambiaConRolesAreasYVersion_YNoConElOrdenDeLosClaims()
    {
        var (_, _, factory) = Armar();
        var baseClave = factory.ClaveDeIdentidad(Principal("Ana", roles: ["Admin", "Editor"], areas: [36, 12]));

        Assert.AreEqual(baseClave, factory.ClaveDeIdentidad(Principal("Ana", roles: ["Editor", "Admin"], areas: [12, 36])));
        Assert.AreNotEqual(baseClave, factory.ClaveDeIdentidad(Principal("Ana", roles: ["Editor"], areas: [36, 12])));
        Assert.AreNotEqual(baseClave, factory.ClaveDeIdentidad(Principal("Ana", roles: ["Admin", "Editor"], areas: [36])));
        Assert.AreNotEqual(baseClave, factory.ClaveDeIdentidad(Principal("Ana", roles: ["Admin", "Editor"], areas: [36, 12], version: "8")));
        Assert.AreNotEqual(baseClave, factory.ClaveDeIdentidad(Principal("Bruno", roles: ["Admin", "Editor"], areas: [36, 12])));
        Assert.AreEqual(string.Empty, factory.ClaveDeIdentidad(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
