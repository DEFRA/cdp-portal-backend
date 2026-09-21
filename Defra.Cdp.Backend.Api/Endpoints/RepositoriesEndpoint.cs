using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class RepositoriesEndpoint
{

    public static void MapRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/repositories/{id}", GetRepositoryById);
        app.MapGet("/check-repository-exists/{id}", GetRepoExists);
    }

    private static async Task<Results<NotFound<ApiError>,Ok<Repository>>> GetRepositoryById(IRepositoryService repositoryService, string id,
        CancellationToken cancellationToken)
    {
        var maybeRepository = await repositoryService.FindRepositoryById(id, cancellationToken);
        return maybeRepository == null
            ? TypedResults.NotFound(new ApiError($"{id} not found"))
            : TypedResults.Ok(maybeRepository);
    }
    
    private static async Task<Results<NotFound<ApiError>,Ok>> GetRepoExists(
        IGithubApiService githubApiService, string id,
        CancellationToken cancellationToken)
    {
        return await githubApiService.DoesRepoExist(id, cancellationToken) switch
        {
            false => TypedResults.NotFound(new ApiError($"{id} doesnt exist")),
            true => TypedResults.Ok()
        };
    }
    
}