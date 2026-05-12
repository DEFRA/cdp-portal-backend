using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

public static class AuthPolicies
{
    /// <summary>
    /// Requires logged-in user to have permission:admin role in User Service Backend
    /// </summary>
    public static readonly AuthorizationPolicy IsAdmin = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("cdp", CdpScopes.Admin)
        .Build();

    /// <summary>
    /// Requires logged-in user to have permission:tenant or permission:admin role in User Service Backend.
    /// </summary>
    public static readonly AuthorizationPolicy IsTenant = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("cdp", CdpScopes.Admin, CdpScopes.Tenant)
        .Build();
}

/// <summary>
/// Copied from cdp-libraries/cdp-validation-kit
/// </summary>
public static class CdpScopes
{
    public const string Admin = "permission:admin";
    public const string BreakGlass = "permission:breakGlass";
    public const string CanGrantBreakGlass = "permission:canGrantBreakGlass";
    public const string ExternalTest = "permission:externalTest";
    public const string RestrictedTechPostgres = "permission:restrictedTechPostgres";
    public const string RestrictedTechPython = "permission:restrictedTechPython";
    public const string ServiceOwner = "permission:serviceOwner";
    public const string Tenant = "permission:tenant";
    public const string TestAsTenant = "permission:testAsTenant";
}