using System.Net.Http.Headers;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Utils.Clients;

record CdpScopes
{
    public List<string> scopes { get; set; }
    
}

public class UserServiceBackendClient {

    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public UserServiceBackendClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = configuration.GetValue<string>("UserServiceBackendUrl")!;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new ArgumentNullException("userServiceBackendUrl", "User service backend url cannot be null");
        _client = httpClientFactory.CreateClient("UserServiceBackendClient");
    }
    
    public async Task<IReadOnlyList<string>> GetScopesForUser(string accessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/scopes");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        
        using var res = await _client.SendAsync(req, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return [];
        res.EnsureSuccessStatusCode();

        // Expecting: ["data.read","data.write","admin"] etc.
        var perms = await res.Content.ReadFromJsonAsync<CdpScopes>(cancellationToken: ct);
                    
        return perms?.scopes ?? [];
    }
    
    public async Task<UserServiceUser?> GetUser(string userId, CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl + "/users/" + userId, cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UserServiceUser?>(response, cancellationToken: cancellationToken);
    }
}