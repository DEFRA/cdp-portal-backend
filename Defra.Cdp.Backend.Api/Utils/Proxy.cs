using System.Net;
using Serilog.Core;

namespace Defra.Cdp.Backend.Api.Utils;

public static class Proxy
{
    public const string ProxyClient = "proxy";
    
    public static void AddHttpProxyClient(this IServiceCollection services, Logger logger)
    {
        services.AddHttpClient(ProxyClient).ConfigurePrimaryHttpMessageHandler(() =>
        {
            // Note: HTTPS proxy isn't support in dotnet until dotnet 8
            var proxyUri = Environment.GetEnvironmentVariable("CDP_HTTP_PROXY");
            var proxy = new WebProxy
            {
                BypassProxyOnLocal = true
            };
            if (proxyUri != null)
            {
                var uri = new Uri(proxyUri);
                logger.Information("Creating proxy http client {uri}", RedactUriCredentials(uri));
                proxy.Address = uri;

                var credentials = GetCredentialsFromUri(uri) ?? GetCredentialsFromEnv();
                if (credentials != null)
                {
                    logger.Information("Setting proxy credentials");
                    proxy.Credentials = credentials;
                }
            }
            else
            {
                logger.Warning("CDP_HTTP_PROXY is NOT set, proxy client will be disabled");
            }
            return new HttpClientHandler { Proxy = proxy, UseProxy = proxyUri != null};
        });
    }
    
    public static NetworkCredential? GetCredentialsFromUri(Uri uri)
    {
        var split = uri.UserInfo.Split(':');
        return split.Length == 2 ? new NetworkCredential(split[0], split[1]) : null;
    }
    
    private static NetworkCredential? GetCredentialsFromEnv()
    {
        var proxyUsername = Environment.GetEnvironmentVariable("SQUID_USERNAME");
        var proxyPassword = Environment.GetEnvironmentVariable("SQUID_PASSWORD");
        if (proxyUsername != null && proxyPassword != null)
        {
            return new NetworkCredential(proxyUsername, proxyPassword);
        }

        return null;
    }
    
    public static string RedactUriCredentials(Uri uri)
    {
        var uriBuilder = new UriBuilder(uri);
        if (!string.IsNullOrEmpty(uriBuilder.Password))
        {
            uriBuilder.Password = "*****";
        }
        return uriBuilder.Uri.ToString();
    }
}