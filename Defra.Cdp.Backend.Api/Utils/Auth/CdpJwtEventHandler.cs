using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

/// <summary>
/// Enriches the HttpContext Principal with the claims requested from cdp-user-service-backend
/// </summary>
/// <param name="permissionsClient"></param>
public class CdpJwtEventHandler(ICdpPermissionsClient permissionsClient) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                     context.Principal?.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Fail("User ID claim not found in token.");
            return;
        }

        try
        {
            var additionalClaims =
                await permissionsClient.ScopesForUser(userId, context.SecurityToken,
                    context.HttpContext.RequestAborted);
            var appIdentity = new ClaimsIdentity(additionalClaims);
            context.Principal!.AddIdentity(appIdentity);
        }
        catch (HttpRequestException ex)
        {
            context.Fail($"External API call failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            context.Fail($"Validation failed due to an error: {ex.Message}");
        }

        await base.TokenValidated(context);
    }
}