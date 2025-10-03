using System.Security.Claims;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Microsoft.AspNetCore.Authentication;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

/**
 * Enriches User's claims with scopes pulled from User Service Backend.
 */
public class CdpClaimsTransformer(IHttpContextAccessor httpContextAccessor, UserServiceBackendClient client)
    : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return principal;

        var ctx = httpContextAccessor.HttpContext;
        var token = GetIncomingBearerAsync(ctx);
        if (string.IsNullOrEmpty(token)) return principal;

        var perms = await client.GetScopesForUser(token,  ctx?.RequestAborted ?? CancellationToken.None);

        var id = new ClaimsIdentity("cdp_scope");
        foreach (var p in perms.Distinct())
            id.AddClaim(new Claim("cdp_scope", p));
        principal.AddIdentity(id);

        return principal;
    }
    
    private static string? GetIncomingBearerAsync(HttpContext? ctx)
    {
        if (ctx is null) return null;
        if (!ctx.Request.Headers.TryGetValue("Authorization", out var auth)) return null;
        var value = auth.ToString();
        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return value["Bearer ".Length..].Trim();
        return null;
    }
}