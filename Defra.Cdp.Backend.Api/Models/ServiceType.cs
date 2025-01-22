using System.Collections.Immutable;
using Defra.Cdp.Backend.Api.Services.Github;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record ServiceType(
    string Name,
    string Language,
    string? RequiredScope,
    string Type,
    string Zone)
{
    public ServiceType(ServiceTemplate serviceTemplate) : this(serviceTemplate.Description,
        serviceTemplate.Language,
        serviceTemplate.RequiredScope, serviceTemplate.Type, serviceTemplate.Zone)
    {
    }
}

public sealed record ServiceTypesResult(string Message, ImmutableDictionary<string, ServiceType> ServiceTypes);