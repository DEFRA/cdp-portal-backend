using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class TenantBucketsEndpoint
{
    public static void MapTenantBucketsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tenant-buckets/{service}/{environment}", TenantBuckets);
        app.MapGet("/tenant-buckets/{service}", AllTenantBuckets);
    }

    private static async Task<IResult> TenantBuckets(ITenantBucketsService tenantBucketsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await tenantBucketsService.FindBuckets(service, environment, cancellationToken);
        return result.Count > 0 ? Results.Ok(new TenantBucketResponse(result)) : Results.NotFound(new ApiError("Not found"));
    }

    private static async Task<IResult> AllTenantBuckets(
        ITenantBucketsService tenantBucketsService,
        string service,
        CancellationToken cancellationToken)
    {
        var result = await tenantBucketsService.FindAllBuckets(service, cancellationToken);

        if (result.Count == 0)
        {
            return Results.NotFound(new ApiError("Not found"));
        }

        var response = result.Aggregate(new Dictionary<string, List<TenantBucketRecord>>(), (acc, bucket) =>
        {
            if (acc.TryGetValue(bucket.Environment, out var value))
            {
                value.Add(bucket);
            }
            else
            {
                acc[bucket.Environment] = [bucket];
            }

            return acc;
        }).ToDictionary(pair =>  pair.Key, pair => new TenantBucketResponse(pair.Value ));

        return Results.Ok(response);
    }


    private class TenantBucketResponse(List<TenantBucketRecord> records)
    {
        [JsonPropertyName("buckets")] public List<string> Buckets { get; } = records.Select(r => r.Bucket).ToList();
    }

}