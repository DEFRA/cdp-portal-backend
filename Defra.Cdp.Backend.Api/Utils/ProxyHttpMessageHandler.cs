using System.Net;

namespace Defra.Cdp.Backend.Api.Utils;

public class ProxyHttpMessageHandler : HttpClientHandler
{
    
    public ProxyHttpMessageHandler (ILogger<ProxyHttpMessageHandler> logger)
    {
        var proxyUri = Environment.GetEnvironmentVariable("CDP_HTTPS_PROXY");
        var proxy = new WebProxy
        {
            BypassProxyOnLocal = true
        };
        if (proxyUri != null)
        {
            logger.LogDebug("Creating proxy http client");
            var uri = new UriBuilder(proxyUri);

            var credentials = GetCredentialsFromUri(uri);
            if (credentials != null)
            {
                logger.LogDebug("Setting proxy credentials");
                proxy.Credentials = credentials;    
            }

            // Remove credentials from URI to so they don't get logged.
            uri.UserName = "";
            uri.Password = "";
            proxy.Address = uri.Uri;
        }
        else
        {
            logger.LogWarning("CDP_HTTP_PROXY is NOT set, proxy client will be disabled");
        }

        Proxy = proxy;
        UseProxy = proxyUri != null;

    }

    public static NetworkCredential? GetCredentialsFromUri(UriBuilder uri)
    {
        var username = uri.UserName;
        var password = uri.Password;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return null;
        return new NetworkCredential(username, password);
    }
}