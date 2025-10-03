using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Defra.Cdp.Backend.Api.Utils.Auth.Policies;
using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class CreateEndpoint
{
   
    public static void MapCreateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/create/templates/{team}", ListTemplates);
    }

    static async Task<IResult> ListTemplates(HttpContext ctx, CreateTenantService createService, ILoggerFactory loggerFactory)
    {
        Console.WriteLine($"Listing Templates!! {ctx.User.Identity} {ctx.User.Identity.IsAuthenticated} {ctx.User.Identity.AuthenticationType}" );
        var log = loggerFactory.CreateLogger<CreateTenantService>();
        
        foreach (var userClaim in ctx.User.Claims)
        {
            Console.WriteLine($"Claim {userClaim.Value} {userClaim.Type} {userClaim.Issuer} {userClaim.Subject?.Name}");
        }
        
        return Results.Ok(createService.ListTemplates(ctx));
    }
}