using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public interface IUserServiceBackendClient
{
    Task<List<UserServiceTeam>?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken);
    Task<UserServiceUser?> GetUser(string userId, CancellationToken cancellationToken);
    Task SyncTeams(IEnumerable<UserServiceTeamSync> teams, CancellationToken cancellationToken);
}

public class UserServiceBackendClient : IUserServiceBackendClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public UserServiceBackendClient(IConfiguration configuration, IHttpClientFactory httpClientFactory)
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

    /// <summary>
    /// Syncs teams from our local team cache to User Service Backend
    /// </summary>
    /// <param name="teams"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task SyncTeams(IEnumerable<UserServiceTeamSync> teams, CancellationToken cancellationToken)
    {
        using var result = await _client.PostAsJsonAsync(_baseUrl + "/sync/teams", new { teams = teams }, cancellationToken);
        result.EnsureSuccessStatusCode();
    }
}