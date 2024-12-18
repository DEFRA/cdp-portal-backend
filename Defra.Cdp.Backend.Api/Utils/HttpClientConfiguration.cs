namespace Defra.Cdp.Backend.Api.Utils;

public static class HttpClientConfiguration
{
    public static void Default(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add(
            "Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend");
    }

    public static void GitHub(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add(
            "Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend"); // required by GitHub API or else you get 403
    }

    public static void Proxy(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add(
            "Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend");
    }
}