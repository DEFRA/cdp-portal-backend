using System.Security.Claims;
using Defra.Cdp.Backend.Api.Services.Entities.Model;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

public static class BreakGlass
{
    public static bool HasBreakGlass(ClaimsPrincipal user, Entity? entity = null) {
        if (user.HasClaim("cdp", CdpScopes.BreakGlass)) {
            return true;
        };

        if (entity != null ) {
            return entity.Teams.Any(t => user.HasClaim("cdp", $"{CdpScopes.BreakGlass}:team:{t.TeamId}"));
        }

        return false;
    }
}