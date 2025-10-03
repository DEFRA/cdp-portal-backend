using Defra.Cdp.Backend.Api.Services.Create;
using Microsoft.AspNetCore.Authorization;

namespace Defra.Cdp.Backend.Api.Utils.Auth.Policies;

public static class CdpAuthorizationHandlers
{
    public static IServiceCollection AddCdpAuthHandlers(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IAuthorizationHandler, IsAdminAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, OwnerOfServiceAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, MemberOfTeamAuthorizationHandler>();

        return services;
    }
}