using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Models;

public record ShutteringUrlState
{
    public required string Environment { get; set; }
    public required  string ServiceName { get; set; }
    public required string Url { get; set; }
    public required string Waf { get; set; }
    public bool Internal { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ShutteringStatus Status { get; set; }

    public User? LastActionedBy { get; set; }
    public DateTime? LastActionedAt { get; set; }
}
public enum ShutteringStatus
{
    Shuttered,
    Active,
    PendingShuttered,
    PendingActive
}
