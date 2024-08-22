using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class LibrariesEndpoint
{
    public static IEndpointRouteBuilder MapLibrariesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("libraries",
                async ([FromQuery(Name = "team")] string? team) => { return await GetAllLibraries(team); });

        app.MapGet("libraries/{serviceId}", GetLibrariesByServiceId);

        return app;
    }


    private static Task<IResult> GetAllLibraries(string? team)
    {
        return Task.FromResult(Results.Ok(new LibrariesResult("success", Array.Empty<string>())));
    }

    private static Task<IResult> GetLibrariesByServiceId(string? serviceId)
    {
        return Task.FromResult(Results.Ok(new LibrariesResult("success", Array.Empty<string>())));
    }

    private record LibrariesResult(string Message, string[] Libraries);
}