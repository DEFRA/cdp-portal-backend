namespace Defra.Cdp.Backend.Api.Models;

public sealed record ServiceType(string Value, string Text);

public sealed record ServiceTypesResult(string Message, IEnumerable<ServiceType> ServiceTypes);