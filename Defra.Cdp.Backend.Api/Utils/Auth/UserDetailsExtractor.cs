using System.Security.Claims;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

public static class UserDetailsExtractor
{
    public static UserDetails? UserDetailsFrom(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var id = principal.FindFirst("oid")?.Value ?? principal.Identity?.Name;
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var displayName = principal.FindFirst("name")?.Value ?? "";
        return new UserDetails { Id = id, DisplayName = displayName };
    }
}