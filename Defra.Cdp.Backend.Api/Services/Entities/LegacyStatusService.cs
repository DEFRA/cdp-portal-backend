using System.Reflection;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.LegacyHelpers;
using Defra.Cdp.Backend.Api.Services.GithubEvents;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface ILegacyStatusService
{
    Task Create(LegacyStatus status, CancellationToken cancellationToken);
    Task UpdateField(LegacyStatusUpdateRequest updateRequest, CancellationToken cancellationToken);
    Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken);
    Task<LegacyStatus?> StatusForRepositoryName(string repositoryName, CancellationToken cancellationToken);

    Task UpdateWorkflowStatus(string serviceRepo, string workflowRepo, string headBranch, Status workflowStatus,
        TrimmedWorkflowRun trimWorkflowRun);

    Task<List<LegacyStatus>> FindAllInProgressOrFailed(CancellationToken cancellationToken);
    Task<List<LegacyStatusService.InProgressFilters>> GetInProgressFilters(string? kind, CancellationToken cancellationToken);

    Task<List<LegacyStatus>> GetInProgress(string? service, string? teamId, string? kind,
        CancellationToken cancellationToken);
}

public class LegacyStatusService(
    IMongoDbClientFactory connectionFactory,
    IOptions<GithubOptions> githubOptions,
    ILoggerFactory loggerFactory)
    : MongoService<LegacyStatus>(connectionFactory, CollectionName, loggerFactory), ILegacyStatusService
{
    private const string CollectionName = "legacystatuses";

    protected override List<CreateIndexModel<LegacyStatus>> DefineIndexes(
        IndexKeysDefinitionBuilder<LegacyStatus> builder)
    {
        var repositoryIndex = new CreateIndexModel<LegacyStatus>(builder.Ascending(s => s.RepositoryName),
            new CreateIndexOptions { Unique = true });
        var repositoryStatusIndex =
            new CreateIndexModel<LegacyStatus>(builder.Ascending(s => s.RepositoryName).Ascending(s => s.Status));

        return [repositoryIndex, repositoryStatusIndex];
    }

    public async Task Create(LegacyStatus status, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(status, cancellationToken: cancellationToken);
    }

    public async Task UpdateField(LegacyStatusUpdateRequest updateRequest, CancellationToken cancellationToken)
    {
        var filter = Builders<LegacyStatus>.Filter.Eq(status => status.RepositoryName, updateRequest.repositoryName);

        var update = Builders<LegacyStatus>.Update
            .Set($"{updateRequest.fieldName}.status", updateRequest.detail.status)
            .Set($"{updateRequest.fieldName}.trigger", updateRequest.detail.trigger)
            .Set($"{updateRequest.fieldName}.result", updateRequest.detail.result);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken)
    {
        var filter = Builders<LegacyStatus>.Filter.Eq(status => status.RepositoryName, repositoryName);

        var currentStatus = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (currentStatus == null)
        {
            return;
        }

        var updatedStatus = CalculateOverallStatus(currentStatus);
        var update = Builders<LegacyStatus>.Update
            .Set(s => s.Status, updatedStatus.ToStringValue());
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<LegacyStatus?> StatusForRepositoryName(string repositoryName, CancellationToken cancellationToken)
    {
        return await Collection.Find(s => s.RepositoryName == repositoryName).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateWorkflowStatus(string serviceRepo, string workflowRepo, string branch,
        Status workflowStatus,
        TrimmedWorkflowRun trimWorkflowRun)
    {
        var filter = Builders<LegacyStatus>.Filter.And(
            Builders<LegacyStatus>.Filter.Eq(s => s.RepositoryName, serviceRepo),
            Builders<LegacyStatus>.Filter.Nin(
                $"{workflowRepo}.status", StatusHelper.DontOverwriteStatus(workflowStatus)
            )
        );

        var update = Builders<LegacyStatus>.Update
            .Set($"{workflowRepo}.status", workflowStatus.ToStringValue())
            .Set($"{workflowRepo}.{branch}.workflow", trimWorkflowRun);

        try
        {
             var result = await Collection.UpdateOneAsync(filter, update);
             
             if (result.MatchedCount == 0)
             {
                 Console.WriteLine("No document matched the filter.");
             }
             else if (result.ModifiedCount > 0)
             {
                 Console.WriteLine("Document updated successfully.");
             }
             else
             {
                 Console.WriteLine("Document matched but no modifications were necessary.");
             }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error updating legacy status");
        }

    }

    public async Task<List<LegacyStatus>> FindAllInProgressOrFailed(CancellationToken cancellationToken)
    {
        var filter = Builders<LegacyStatus>.Filter.In(s => s.Status,
            [Status.InProgress.ToStringValue(), Status.Failure.ToStringValue()]);

        return await Collection.Find(filter).ToListAsync(cancellationToken: cancellationToken);
    }


    private Status CalculateOverallStatus(LegacyStatus statusRecord)
    {
        var statusKeys = StatusHelper.GetStatusKeys(githubOptions.Value.Repos, statusRecord.Kind.ToType());

        var allSuccess = CheckAllKeysWithGivenStatus(statusRecord, statusKeys, Status.Success);

        var anyFailed = CheckAnyKeysWithGivenStatus(statusRecord, statusKeys, Status.Failure);

        if (allSuccess)
        {
            return Status.Success;
        }

        return anyFailed ? Status.Failure : Status.InProgress;
    }

    private static bool CheckAllKeysWithGivenStatus(LegacyStatus statusRecord, List<string> statusKeys, Status status)
    {
        var properties = statusRecord.GetType().GetProperties();
        return statusKeys.All(key =>
        {
            var keyProperty =
                properties.FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == key);

            return ((WorkflowDetails)statusRecord.GetType().GetProperty(keyProperty.Name)?.GetValue(statusRecord))
                   ?.Status.ToStatus() == status;
        });
    }

    private static bool CheckAnyKeysWithGivenStatus(LegacyStatus statusRecord, List<string> statusKeys, Status status)
    {
        var properties = statusRecord.GetType().GetProperties();
        return statusKeys.Any(key =>
        {
            var keyProperty =
                properties.FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == key);

            return ((WorkflowDetails)statusRecord.GetType().GetProperty(keyProperty.Name)?.GetValue(statusRecord))
                   ?.Status.ToStatus() == status;
        });
    }

    public async Task<List<InProgressFilters>> GetInProgressFilters(string? kind, CancellationToken cancellationToken)
    {
        var statuses = new List<string> { Status.InProgress.ToStringValue(), Status.Failure.ToStringValue() };
        var stages = new List<BsonDocument>();

        if (!string.IsNullOrEmpty(kind))
        {
            stages.Add(new BsonDocument { { "$match", new BsonDocument { { "kind", kind } } } });
        }

        stages.Add(new BsonDocument
        {
            { "$match", new BsonDocument { { "status", new BsonDocument { { "$in", new BsonArray(statuses) } } } } }
        });

        stages.Add(new BsonDocument
        {
            {
                "$group",
                new BsonDocument
                {
                    { "_id", new BsonDocument() },
                    { "services", new BsonDocument { { "$addToSet", "$repositoryName" } } },
                    { "teams", new BsonDocument { { "$addToSet", "$team" } } }
                }
            }
        });

        stages.Add(new BsonDocument
        {
            {
                "$project",
                new BsonDocument
                {
                    { "_id", 0 },
                    { "services", 1 },
                    { "teams", new BsonDocument { { "teamId", 1 }, { "name", 1 } } }
                }
            }
        });

        return await Collection.Aggregate<InProgressFilters>(stages).ToListAsync(cancellationToken) ?? [];
    }
    
    public class InProgressFilters
    {
        public List<string> Services { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
    }

    public class Team
    {
        public Guid TeamId { get; set; }
        public string Name { get; set; }
    }

    public async Task<List<LegacyStatus>> GetInProgress(string? service, string? teamId, string? kind,
        CancellationToken cancellationToken)
    {
        var statuses = new List<string> { Status.InProgress.ToStringValue(), Status.Failure.ToStringValue() };

        var stages = new List<BsonDocument>
        {
            new()
            {
                { "$match", new BsonDocument { { "status", new BsonDocument { { "$in", new BsonArray(statuses) } } } } }
            }
        };

        if (!string.IsNullOrEmpty(kind))
        {
            stages.Add(new BsonDocument { { "$match", new BsonDocument { { "kind", kind } } } });
        }

        if (!string.IsNullOrEmpty(teamId))
        {
            stages.Add(new BsonDocument { { "$match", new BsonDocument { { "team.teamId", teamId } } } });
        }

        if (!string.IsNullOrEmpty(service))
        {
            stages.Add(new BsonDocument
            {
                {
                    "$match",
                    new BsonDocument
                    {
                        {
                            "$or",
                            new BsonArray
                            {
                                new BsonDocument
                                {
                                    {
                                        "repositoryName",
                                        new BsonDocument { { "$regex", service }, { "$options", "i" } }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        stages.Add(new BsonDocument { { "$project", new BsonDocument { { "_id", 0 } } } });

        return await Collection.Aggregate<LegacyStatus>(stages, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }
}

public record LegacyStatusUpdateRequest(string repositoryName, string fieldName, UpdateDetail detail);

public record UpdateDetail(string status, UpdateDetailTrigger trigger, string result);

public record UpdateDetailTrigger(string org, string repo, string workflow, Dictionary<string, string> inputs);
