namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

public record EntityStatus(Entity Entity, Dictionary<string, bool> Resources);