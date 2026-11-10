using System.Security.Claims;
using IVC.Application.Identidad;
using IVC.Services.Abstractions;
using IVC.Services.Implementations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests.Services;

/// <summary>
/// Caché del usuario por circuito (reemplaza a Session["Usuario"] del legado, ver
/// UserHelperSesionTests): se reutiliza sólo para la misma identidad efectiva y se
/// reconstruye cuando cambia la persona, sus roles, sus áreas o la versión de permisos.
/// </summary>
[TestClass]
public class UsuarioActualServiceTests
{
    private sealed class ProveedorFalso : AuthenticationStateProvider
    {
        public ClaimsPrincipal Principal { get; set; } = new(new ClaimsIdentity());
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(Principal));
    }

    private static ClaimsPrincipal Principal(string nombre, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1"), new(ClaimTypes.Name, nombre) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Identity.Application"));
    }

    private static UsuarioIvc Usuario(string nombre, bool admin) => new(
        true, "1", nombre, null, admin, Array.Empty<int>(), Array.Empty<int>(), null, null, false);

    private static (ProveedorFalso Auth, Mock<IUsuarioIvcFactory> Factory, UsuarioActualService Servicio) Armar()
    {
        var auth = new ProveedorFalso();
        var factory = new Mock<IUsuarioIvcFactory>();
        // Clave real: nombre + roles ordenados. Lo que importa es que distinga identidades.
        factory.Setup(f => f.ClaveDeIdentidad(It.IsAny<ClaimsPrincipal>()))
            .Returns((ClaimsPrincipal p) => p.Identity?.IsAuthenticated == true
                ? p.FindFirst(ClaimTypes.Name)!.Value + "|" + string.Join(",", p.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(v => v))
                : string.Empty);
        factory.Setup(f => f.ConstruirAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal p, CancellationToken _) => p.Identity?.IsAuthenticated == true
                ? Usuario(p.FindFirst(ClaimTypes.Name)!.Value, p.IsInRole("Admin"))
                : UsuarioIvc.Anonimo);
        return (auth, factory, new UsuarioActualService(auth, factory.Object));
    }

    [TestMethod]
    public async Task SinPrincipalAutenticado_DevuelveAnonimo()
    {
        var (_, _, servicio) = Armar();
        var usuario = await servicio.ObtenerAsync();
        Assert.IsFalse(usuario.Autenticado);
    }

    [TestMethod]
    public async Task MismaIdentidad_ConstruyeUnaVezYReutiliza()
    {
        var (auth, factory, servicio) = Armar();
        auth.Principal = Principal("Ana", "Admin");

        var primero = await servicio.ObtenerAsync();
        var segundo = await servicio.ObtenerAsync();

        Assert.AreSame(primero, segundo);
        Assert.IsTrue(primero.EsAdmin);
        factory.Verify(f => f.ConstruirAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CambioDeRoles_Reconstruye()
    {
        var (auth, factory, servicio) = Armar();
        auth.Principal = Principal("Ana", "Admin");
        var admin = await servicio.ObtenerAsync();

        auth.Principal = Principal("Ana"); // misma persona, sin rol Admin
        var normal = await servicio.ObtenerAsync();

        Assert.AreNotSame(admin, normal);
        Assert.IsFalse(normal.EsAdmin);
        factory.Verify(f => f.ConstruirAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task OtraPersona_Reconstruye()
    {
        var (auth, _, servicio) = Armar();
        auth.Principal = Principal("Ana");
        await servicio.ObtenerAsync();

        auth.Principal = Principal("Bruno");
        var usuario = await servicio.ObtenerAsync();

        Assert.AreEqual("Bruno", usuario.Nombre);
    }

    [TestMethod]
    public async Task Invalidar_FuerzaLaReconstruccionAunqueLaIdentidadNoCambie()
    {
        var (auth, factory, servicio) = Armar();
        auth.Principal = Principal("Ana");
        await servicio.ObtenerAsync();

        servicio.Invalidar();
        await servicio.ObtenerAsync();

        factory.Verify(f => f.ConstruirAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task PedidosConcurrentes_ConstruyenUnaSolaVez()
    {
        var (auth, factory, servicio) = Armar();
        auth.Principal = Principal("Ana");

        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => servicio.ObtenerAsync()));

        Assert.IsTrue(resultados.All(r => ReferenceEquals(r, resultados[0])));
        factory.Verify(f => f.ConstruirAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
