using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class LibrariesEndpoint
{
    private const string LibrariesBaseRoute = "libraries";
    private const string LibrariesTag = "Libraries";

    public static IEndpointRouteBuilder MapLibrariesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(LibrariesBaseRoute,
                async ([FromQuery(Name = "team")] string? team) => { return await GetAllLibraries(team); })
            .WithName("GetAllLibraries")
            .Produces<LibrariesResult>()
            .Produces(StatusCodes.Status404NotFound) // should be 501 but we don't want to break frontend
            .WithTags(LibrariesTag);

        app.MapGet($"{LibrariesBaseRoute}/{{serviceId}}", GetLibrariesByServiceId)
            .WithName("GetLibrariesByServiceId")
            .Produces<LibrariesResult>()
            .Produces(StatusCodes.Status404NotFound) // should be 501 but we don't want to break frontend
            .WithName(LibrariesTag);

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