using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Models;

[BsonIgnoreExtraElements]
public record ShutteringUrlState
{
    public required string Environment { get; set; }
    public required string ServiceName { get; set; }
    public required string Url { get; set; }
    public string? Waf { get; set; }
    public string? UrlType { get; set; }
    public bool Internal { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ShutteringStatus Status { get; set; }

    public UserDetails? LastActionedBy { get; set; }
    public DateTime? LastActionedAt { get; set; }
}

public enum ShutteringStatus
{
    Shuttered,
    Active,
    PendingShuttered,
    PendingActive
}

public static class ShutterUrlType
{
    public const string FrontendVanityUrl = "frontend_vanity_url";
    public const string ApiGatewayVanityUrl =  "apigw_vanity_url";
}