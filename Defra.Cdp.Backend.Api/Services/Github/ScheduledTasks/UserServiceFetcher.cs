using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class UserServiceFetcher
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public UserServiceFetcher(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = configuration.GetValue<string>("UserServiceBackendUrl")!;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new ArgumentNullException("userServiceBackendUrl", "User service backend url cannot be null");
        _client = httpClientFactory.CreateClient("DefaultClient");
    }

    public async Task<UserServiceRecord?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl + "/teams", cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UserServiceRecord>(response, cancellationToken: cancellationToken);
    }
}