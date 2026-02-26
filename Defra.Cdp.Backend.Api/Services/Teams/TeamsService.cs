using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Teams;

public interface ITeamsService
{
    Task CreateTeam(Team team, CancellationToken cancellationToken = default);
    Task<bool> UpdateTeam(Team team, CancellationToken cancellationToken = default);
    Task<bool> DeleteTeam(string teamId, CancellationToken cancellationToken = default);
    Task SyncTeams(IList<Team> teams, CancellationToken cancellationToken = default);
    Task<List<Team>> FindAll(CancellationToken cancellationToken = default);
    Task<Team?> Find(string teamId, CancellationToken cancellationToken = default);
}


/// <summary>
/// Basic CRUD operations for the Team repo.
/// </summary>
/// <param name="connectionFactory"></param>
/// <param name="loggerFactory"></param>
public class TeamsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<Team>(connectionFactory, CollectionName, loggerFactory), ITeamsService
{
    public const string CollectionName = "teams";

    protected override List<CreateIndexModel<Team>> DefineIndexes(IndexKeysDefinitionBuilder<Team> builder)
    {
        var teamIdIndex = new CreateIndexModel<Team>(builder.Ascending(t => t.TeamId), new CreateIndexOptions { Unique = true });
        return [teamIdIndex];
    }

    /// <summary>
    /// Creates a new team.
    /// Consider adding a pending flag or something to indicate its not been synced yet. 
    /// </summary>
    /// <param name="team"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task CreateTeam(Team team, CancellationToken cancellationToken = default)
    {
        var exists = await Collection.Find(t => t.TeamId == team.TeamId)
            .Limit(1)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"Team {team.TeamId} already exists.");
        }

        await Collection.InsertOneAsync(team, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates a Team. Requires it to be synced to cdp-tenant-config else the changes will be reverted
    /// next sync.
    /// </summary>
    /// <param name="team"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> UpdateTeam(Team team, CancellationToken cancellationToken = default)
    {
        var update = Builders<Team>.Update
            .Set(t => t.TeamName, team.TeamName)
            .Set(t => t.Description, team.Description)
            .Set(t => t.ServiceCode, team.ServiceCode)
            .Set(t => t.Github, team.Github)
            .Set(t => t.SlackChannels, team.SlackChannels);

        var result = await Collection.UpdateOneAsync(
            t => t.TeamId == team.TeamId,
            update,
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    /// <summary>
    /// Deletes a team. Requires the team file to be removed from cdp-tenant-config as well
    /// else it will just resync.
    /// Consider a soft-delete flag or something
    /// </summary>
    /// <param name="teamId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> DeleteTeam(string teamId, CancellationToken cancellationToken = default)
    {
        var result = await Collection.DeleteOneAsync(t => t.TeamId == teamId, cancellationToken);
        return result.DeletedCount > 0;
    }

    /// <summary>
    /// Bulk-updates teams from an external source.
    /// </summary>
    /// <param name="teams"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentException"></exception>
    public async Task SyncTeams(IList<Team> teams, CancellationToken cancellationToken = default)
    {
        
        var existingTeams = await Collection.Find(FilterDefinition<Team>.Empty)
            .ToListAsync(cancellationToken);

        var existingById = existingTeams.ToDictionary(t => t.TeamId);
        var incomingIds = teams.Select(t => t.TeamId).ToHashSet();

        var upserts = teams.Select(team =>
        {
            if (existingById.TryGetValue(team.TeamId, out var existing))
            {
                team = team with { Created = existing.Created };
            }

            var filter = Builders<Team>.Filter.Eq(t => t.TeamId, team.TeamId);
            return new ReplaceOneModel<Team>(filter, team) { IsUpsert = true };
        }).ToList();

        // Prevent issues in the message payload etc blanket-deleting the teams.
        if (upserts.Count == 0)
        {
            throw new ArgumentException("refusing to sync an empty team list.");
        }
        
        await Collection.BulkWriteAsync(upserts, new BulkWriteOptions { IsOrdered = false }, cancellationToken);

        var removedIds = existingTeams.Select(t => t.TeamId)
            .Where(id => !incomingIds.Contains(id))
            .ToList();

        if (removedIds.Count > 0)
        {
            var deleteFilter = Builders<Team>.Filter.In(t => t.TeamId, removedIds);
            await Collection.DeleteManyAsync(deleteFilter, cancellationToken);
        }
    }

    /// <summary>
    /// Finds all the teams
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<Team>> FindAll(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(FilterDefinition<Team>.Empty).Sort(Builders<Team>.Sort.Ascending(t => t.TeamId)).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Finds a single team by teamId
    /// </summary>
    /// <param name="teamId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Team?> Find(string teamId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(t => t.TeamId == teamId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}