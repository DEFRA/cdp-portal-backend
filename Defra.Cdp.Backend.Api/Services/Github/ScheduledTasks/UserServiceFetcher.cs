using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public interface IUserServiceFetcher 
{
    Task<UserServiceTeamResponse?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken);
    Task<UserServiceUserResponse?> GetUser(string userId, CancellationToken cancellationToken);
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
        _client = httpClientFactory.CreateClient("DefaultClient");
    }

    public async Task<UserServiceTeamResponse?> GetLatestCdpTeamsInformation(CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl + "/teams", cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UserServiceTeamResponse>(response, cancellationToken: cancellationToken);
    }
    
    public async Task<UserServiceUserResponse?> GetUser(string userId, CancellationToken cancellationToken)
    {
        var result = await _client.GetAsync(_baseUrl +  "/users/" + userId, cancellationToken);
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UserServiceUserResponse>(response, cancellationToken: cancellationToken);
    }
}