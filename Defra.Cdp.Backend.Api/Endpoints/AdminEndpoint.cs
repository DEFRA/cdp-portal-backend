using Defra.Cdp.Backend.Api.Services.Aws;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class AdminEndpoint
{
    private const string AdminBaseRoute = "admin";
    private const string AdminTag = "Admin";

    public static IEndpointRouteBuilder MapAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost($"{AdminBaseRoute}/backfill", Backfill)
            .WithName("PostAdminBackfill")
            .Produces(StatusCodes.Status200OK) //Todo change 
            .WithTags(AdminTag);

        return app;
    }

    public static async Task Backfill(EcsEventListener eventListener, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("AdminEndpoint");
        logger.LogInformation("Starting back-fill operation");
        await eventListener.Backfill();
    }
}