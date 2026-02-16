using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Defra.Cdp.Backend.Api.Services.Entities;

namespace Defra.Cdp.Backend.Api.Services.Migrations;

public record MigrationVersion
{
    [property: JsonPropertyName("created")]
    public required DateTime Created { get; init; }

    [property: JsonPropertyName("version")]
    public required string Version { get; init; }

    [property: JsonPropertyName("path")]
    public required string Path { get; init; }

    [property: JsonPropertyName("kind")] public string Kind { get; init; } = "liquibase";

}

public interface IAvailableMigrations
{
    public Task<List<MigrationVersion>> FindMigrationsForService(string service, CancellationToken ct);
    public Task<List<string>> FindServicesWithMigrations(CancellationToken ct);
    public Task<List<string>> FindServicesWithMigrationsByTeam(List<string> teamIds, CancellationToken ct);
}

public class AvailableMigrations(IAmazonS3 client, IEntitiesService entityService, IConfiguration configuration) : IAvailableMigrations
{
    private readonly string? _bucketName = configuration.GetValue<string>("MigrationsBucket");

    private const string MigrationFileName = "migrations.tgz";

    public async Task<List<MigrationVersion>> FindMigrationsForService(string service, CancellationToken ct)
    {
        if (_bucketName == null)
        {
            throw new Exception("Config error: MigrationsBucket has not been set");
        }

        var prefix = service + "/";
        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = prefix
        };

        List<MigrationVersion> migrations = [];
        ListObjectsV2Response response;
        do
        {
            response = await client.ListObjectsV2Async(request, ct);

            foreach (var s3Object in response.S3Objects)
            {
                var version = ExtractVersion(s3Object.Key);
                if (s3Object.Key.EndsWith(MigrationFileName) && version != null)
                {
                    migrations.Add(new MigrationVersion
                    {
                        Created = s3Object.LastModified ?? DateTime.Now,
                        Version = version,
                        Path = s3Object.Key
                    });
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        return migrations.OrderByDescending(d => d.Created).ToList();
    }

    public async Task<List<string>> FindServicesWithMigrations(CancellationToken ct)
    {
        if (_bucketName == null)
        {
            throw new Exception("Config error: MigrationsBucket has not been set");
        }

        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Delimiter = "/"
        };

        List<string> services = [];
        ListObjectsV2Response response;
        do
        {

            response = await client.ListObjectsV2Async(request, ct);

            services.AddRange(response.CommonPrefixes);
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        return services.Distinct().Select(s => s.Replace("/", "")).ToList();
    }



    public async Task<List<string>> FindServicesWithMigrationsByTeam(List<string> teamIds, CancellationToken ct)
    {
        var migrations = await FindServicesWithMigrations(ct);

        var servicesForTeams = new HashSet<string>();

        foreach (var teamsId in teamIds)
        {
            var tenants = await entityService.GetEntities(new EntityMatcher(TeamId: teamsId, HasPostgres: true), ct);
            foreach (var tenant in tenants)
            {
                servicesForTeams.Add(tenant.Name);
            }
        }

        return migrations.Where(r => servicesForTeams.Contains(r)).ToList();
    }


    private static string? ExtractVersion(string key)
    {
        var parts = key.Split("/");
        return parts.Length > 2 ? parts[1] : null;
    }
}