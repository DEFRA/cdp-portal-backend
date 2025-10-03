using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

public class CdpPolicyProvider :IAuthorizationPolicyProvider
{
    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var isAdmin = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim("cdp_scope", "permission:admin")
            .Build();
            
            var isMemberOfTeam = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("cdp_scope", ["permission:admin", "team:foo"])
                .Build();
        
        return AuthorizationPolicy.Combine();
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        throw new NotImplementedException();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        throw new NotImplementedException();
    }
}
