namespace Defra.Cdp.Backend.Api.Config;

public static class Environment
{
    public static bool IsDevMode(this WebApplicationBuilder builder)
    {
        return builder.Environment.IsDevelopment() || builder.Environment.EnvironmentName.ToLower().StartsWith("dev");
    }
}