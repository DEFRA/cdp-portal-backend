namespace Defra.Cdp.Backend.Api.Config;

public class MongoConfig
{
    public required string DatabaseUri { get; init; }
    public required string DatabaseName { get; init; }
}