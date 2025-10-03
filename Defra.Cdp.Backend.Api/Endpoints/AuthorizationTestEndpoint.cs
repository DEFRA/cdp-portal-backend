using System.Security.Claims;
using Defra.Cdp.Backend.Api.Services.Audit;
using Defra.Cdp.Backend.Api.Utils.Auth.Policies;
using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AuthorizationTestEndpoint
{
   
    public static void MapAuthTestEndpoint(this IEndpointRouteBuilder app)
    {
        var adminOnlyPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new IsAdmin())
            .Build();

        app.MapGet("/auth-test/is-admin", CheckIsAdmin)
            .RequireAuthorization(adminOnlyPolicy).RequireAuthorization();
        

        app.MapGet("/auth-test/is-owner-of/{name}", IsOwnerOfEntity)
            .RequireAuthorization(b => 
                b.RequireAuthenticatedUser()
                    .AddRequirements(new OwnerOfEntity("name"))
                    .Build());
       
    }

    private static string printAuth(ClaimsPrincipal? user)
    {
        var userName = user.Identity?.Name ?? "No user name";
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        var claims =
            user.Claims.Select(claim => $"Type: {claim.Type} Value: {claim.Value}, Issuer: {claim.Issuer}");
        return $"{userName}, authenticated: {isAuthenticated}\n{string.Join('\n', claims)}";

    }

    private static IResult CheckIsAdmin(HttpContext httpContext)
    {
        return Results.Ok(printAuth(httpContext.User));
    }

    private static IResult IsOwnerOfEntity(string name, HttpContext httpContext)
    {
        Console.WriteLine($"Checking owner of {name}");
        return Results.Ok(printAuth(httpContext.User));
    }


}