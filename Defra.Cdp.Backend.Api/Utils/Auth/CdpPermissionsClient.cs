using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Defra.Cdp.Backend.Api.Utils.Auth;

public interface ICdpPermissionsClient
{
    Task<IEnumerable<Claim>> ScopesForUser(string userId, SecurityToken token, CancellationToken cancellationToken);
}

/// <summary>
/// Calls cdp-user-service-backend, forwarding a user's jwt token and returns a list of scopes/permissions.
/// </summary>
/// <param name="client"></param>
/// <param name="configuration"></param>
public class CdpPermissionsClient(HttpClient client, IConfiguration configuration) : ICdpPermissionsClient
{
    private readonly string _baseUrl = configuration.GetValue<string>("UserServiceBackendUrl")!;

    public async Task<IEnumerable<Claim>> ScopesForUser(string userId, SecurityToken token,
        CancellationToken cancellationToken)
    {
        var uri = new UriBuilder(_baseUrl) { Path = "/scopes" }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.UnsafeToString());

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        var scopes = await response.Content.ReadFromJsonAsync<ScopeResponse>(cancellationToken);
        return scopes?.Scopes.Select(s => new Claim("cdp", s)).ToList() ?? [];
    }
}

public record ScopeResponse
{
    [JsonPropertyName("scopes")] public required List<string> Scopes { get; init; }
}