#nullable enable

namespace IVC.Security;

/// <summary>
/// Validación del <c>returnUrl</c> que IVC reenvía al login de Auth.Web. El middleware de la
/// cookie siempre arma una ruta relativa de esta misma app ("/obras"); cualquier otra cosa
/// (otro host, esquema, "//host" o caracteres de control) no es un destino válido y se
/// reemplaza por el inicio. Auth.Web valida el destino de nuevo contra sus AllowedReturnUrls:
/// esto sólo evita mandarle un valor que ya se sabe inválido.
/// </summary>
public static class ReturnUrls
{
    /// <summary>True si la URL es una ruta relativa local ("/algo" o "/"), sin host ni esquema.</summary>
    public static bool EsLocal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (url[0] != '/') return false;
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\')) return false;
        foreach (var c in url)
        {
            if (char.IsControl(c)) return false;
        }
        return true;
    }
}
