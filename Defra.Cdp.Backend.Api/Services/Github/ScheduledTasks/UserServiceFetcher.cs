using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public interface IUserServiceFetcher
{
    Task<List<UserServiceTeam>?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken);
    Task<UserServiceUser?> GetUser(string userId, CancellationToken cancellationToken);
}

public class UserServiceFetcher : IUserServiceFetcher
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public UserServiceFetcher(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = configuration.GetValue<string>("UserServiceBackendUrl")!;
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new ArgumentNullException("userServiceBackendUrl", "User service backend url cannot be null");
        _client = httpClientFactory.CreateClient("ServiceClient");
    }

    public async Task<List<UserServiceTeam>?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl + "/teams", cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<UserServiceTeam>>(response, cancellationToken: cancellationToken);
    }

    public async Task<UserServiceUser?> GetUser(string userId, CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl + "/users/" + userId, cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UserServiceUser?>(response, cancellationToken: cancellationToken);
    }
}