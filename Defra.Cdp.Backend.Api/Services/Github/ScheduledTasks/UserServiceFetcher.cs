using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class UserServiceFetcher
{
    private readonly HttpClient _client;

    public UserServiceFetcher(IConfiguration configuration)
    {
        var userServiceBackendUrl = configuration.GetValue<string>("UserServiceBackendUrl")!;
        if (string.IsNullOrWhiteSpace(userServiceBackendUrl))
            throw new ArgumentNullException("userServiceBackendUrl", "User service backend url cannot be null");
        _client = new HttpClient();
        _client.BaseAddress = new Uri(userServiceBackendUrl);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Add(
            "Accept", "application/json");
        _client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend");
    }

    public async Task<UserServiceRecord?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync("/cdp-user-service-backend/teams", cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UserServiceRecord>(response, cancellationToken: cancellationToken);
    }
}