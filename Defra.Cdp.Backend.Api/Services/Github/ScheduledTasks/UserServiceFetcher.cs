using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class UserServiceFetcher
{
    private readonly HttpClient _client;
    private readonly string _userServiceBackendUrl;

    public UserServiceFetcher(IConfiguration configuration)
    {
        _userServiceBackendUrl = configuration.GetValue<string>("UserServiceBackendUrl")!;
        if (_userServiceBackendUrl == null)
            throw new ArgumentNullException("_userServiceBackendUrl", "User service backend url cannot be null");
        // TODO REMOVE ME BEFORE MERGING IN
        var handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback =
            (httpRequestMessage, cert, cetChain, policyErrors) => { return true; };
        _client = new HttpClient(handler);
        _client.BaseAddress = new Uri(_userServiceBackendUrl);
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Add(
            "Accept", "application/json");
        _client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend");
    }

    public async Task<UserServiceRecord?> getLatestCdpTeamsInformation()
    {
        var result = await _client.GetAsync("/cdp-user-service-backend/teams");
        result.EnsureSuccessStatusCode();
        var response = await result.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<UserServiceRecord>(response);
    }
}