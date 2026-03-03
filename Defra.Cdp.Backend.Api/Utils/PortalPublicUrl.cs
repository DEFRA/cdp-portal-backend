namespace Defra.Cdp.Backend.Api.Utils;

public static class PortalPublicUrl
{
    public static Uri BaseUri()
    {
        // TODO: we could look this up from portal-frontend's entity/vanity url data...
        var env = Environment.GetEnvironmentVariable("ENVIRONMENT");
        if (env == CdpEnvironments.InfraDev)
        {
            return new Uri("https://portal-test.cdp-int.defra.cloud");
        }

        return new Uri("https://portal.cdp-int.defra.cloud");
    }
}