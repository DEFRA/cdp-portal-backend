using System.Net;

namespace Defra.Cdp.Backend.Api.Utils;

public class ProxyHttpMessageHandler : HttpClientHandler
{
    public ProxyHttpMessageHandler(ILogger<ProxyHttpMessageHandler> logger)
    {
        var proxyUri = Environment.GetEnvironmentVariable("HTTP_PROXY");
        var proxy = new WebProxy { BypassProxyOnLocal = true };
        if (proxyUri != null)
        {
            logger.LogDebug("Creating proxy http client");
            var uri = new UriBuilder(proxyUri).Uri;
            proxy.Address = uri;
        }
        else
        {
            logger.LogWarning("HTTP_PROXY is NOT set, proxy client will be disabled");
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