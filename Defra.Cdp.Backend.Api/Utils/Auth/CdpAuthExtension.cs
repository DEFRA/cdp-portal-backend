using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

public record CdpAuthConfig
{
    public string? Authority { get; init; }
    public string? Audience { get; init; }
    public bool RequireHttpsMetadata { get; init; } = false;
    public string NameClaimType { get; init; } = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    public bool ValidateAudience { get; init; } = true;
    public bool ValidateLifetime { get; init; } = true;
    public string? ValidAudiences { get; init; }
}

public static class CdpAuthExtension
{
    public static void AddCdpAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var cfg = configuration.GetSection("auth").Get<CdpAuthConfig>();

        if (cfg == null)
        {
            throw new Exception("No auth config found");
        }

        services.AddHttpClient<ICdpPermissionsClient, CdpPermissionsClient>();
        services.AddScoped<ICdpPermissionsClient, CdpPermissionsClient>();
        services.AddScoped<CdpJwtEventHandler>();
        
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = cfg.Authority;
                options.RequireHttpsMetadata = cfg.RequireHttpsMetadata;
                options.Audience = cfg.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = cfg.NameClaimType,
                    ValidateAudience = cfg.ValidateAudience,
                    ValidAudiences = cfg.ValidAudiences?.Split(","),
                    ValidateLifetime = cfg.ValidateLifetime,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
                options.EventsType = typeof(CdpJwtEventHandler);
                options.BackchannelHttpHandler =
                    new ProxyHttpMessageHandler(NullLogger<ProxyHttpMessageHandler>.Instance);
            });
    }
}