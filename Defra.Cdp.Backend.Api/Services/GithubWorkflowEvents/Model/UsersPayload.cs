using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Users;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class UsersPayload
{
    [JsonPropertyName("users")] public required List<UserPayload> Users { get; init; }
}

public record UserPayload
{
    [JsonPropertyName("user_id")] public required string UserID { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("email")] public required string Email { get; init; }

    [JsonPropertyName("github")] public string? Github { get; init; }

    public User ToUser()
    {
        return new User
        {
            UserId = UserID,
            Name = Name,
            Email = Email,
            Github = Github
        };
    }
}

