using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface IRepositoryService
{
    Task Upsert(Repository repository, CancellationToken cancellationToken);

    Task UpsertMany(IEnumerable<Repository> repositories, CancellationToken cancellationToken);

    Task DeleteUnknownRepos(IEnumerable<string> knownReposIds, CancellationToken cancellationToken);

    Task<List<Repository>> AllRepositories(bool excludeTemplates, CancellationToken cancellationToken);

    Task<List<Repository>> FindRepositoriesByTeam(string team, bool excludeTemplates,
        CancellationToken cancellationToken);

    Task<List<Repository>> FindTeamRepositoriesByTopic(string teamId, string topic,
        CancellationToken cancellationToken);

    Task<List<Repository>> FindRepositoriesByTopic(string topic, CancellationToken cancellationToken);

    Task<Repository?> FindRepositoryById(string id, CancellationToken cancellationToken);
}

public class RepositoryService : MongoService<Repository>, IRepositoryService
{
    public RepositoryService(IMongoDbClientFactory connectionFactory,
        ILoggerFactory loggerFactory) : base(connectionFactory, "repositories", loggerFactory)
    {
    }

    public async Task UpsertMany(IEnumerable<Repository> repositories, CancellationToken cancellationToken)
    {
        // because we constantly refresh the database, we are looking to upsert the record here

        var replaceOneModels =
            repositories.Select(repository =>
            {
                var filter = Builders<Repository>.Filter
                    .Eq(r => r.Id, repository.Id);
                return new ReplaceOneModel<Repository>(filter, repository) { IsUpsert = true };
            }).ToList();

        if (replaceOneModels.Any())
        {
            // BulkWrite fails if its called with a zero length array
            await Collection.BulkWriteAsync(replaceOneModels, new BulkWriteOptions(), cancellationToken);    
        }
    }

    public async Task<List<Repository>> FindRepositoriesByTopic(string topic, CancellationToken cancellationToken)
    {
        var topics = new List<string>() { "cdp", topic };

        var repositories =
            await Collection
                .Find(Builders<Repository>.Filter.All(r => r.Topics, topics))
                .SortBy(r => r.Id)
                .ToListAsync(cancellationToken);

        return repositories;
    }

    public async Task<List<Repository>> FindTeamRepositoriesByTopic(string teamId, string topic,
        CancellationToken cancellationToken)
    {
        var topics = new List<string>() { "cdp", topic };
        var teamFilter = Builders<Repository>.Filter.ElemMatch(r => r.Teams, t => t.TeamId == teamId);
        var topicFilter = Builders<Repository>.Filter.All(r => r.Topics, topics);

        var repositories =
            await Collection
                .Find(teamFilter & topicFilter)
                .SortBy(r => r.Id)
                .ToListAsync(cancellationToken);
        return repositories;
    }


    public async Task<Repository?> FindRepositoryById(string id, CancellationToken cancellationToken)
    {
        var repository =
            await Collection
                .Find(Builders<Repository>.Filter.Eq(r => r.Id, id))
                .FirstOrDefaultAsync(cancellationToken);
        return repository;
    }

    public async Task<List<Repository>> AllRepositories(bool excludeTemplates, CancellationToken cancellationToken)
    {
        var findDefinition = excludeTemplates
            ? Builders<Repository>.Filter.Eq(r => r.IsTemplate, false)
            : Builders<Repository>.Filter.Empty;

        var repositories =
            await Collection
                .Find(findDefinition)
                .SortBy(r => r.Id)
                .ToListAsync(cancellationToken);
        return repositories;
    }

    public async Task<List<Repository>> FindRepositoriesByTeam(string team, bool excludeTemplates,
        CancellationToken cancellationToken)
    {
        var baseFilter = Builders<Repository>.Filter.ElemMatch(r => r.Teams, t => t.Github == team);

        var findDefinition = excludeTemplates
            ? Builders<Repository>.Filter.And(baseFilter, Builders<Repository>.Filter.Eq(r => r.IsTemplate, false))
            : baseFilter;

        var repositories =
            await Collection
                .Find(findDefinition)
                .SortBy(r => r.Id)
                .ToListAsync(cancellationToken);
        return repositories;
    }

    public async Task DeleteUnknownRepos(IEnumerable<string> knownReposIds, CancellationToken cancellationToken)
    {
        var excludingIdsList = knownReposIds.ToList();
        if (excludingIdsList.IsNullOrEmpty()) throw new ArgumentException("excluded repositories cannot be empty");
        await Collection.DeleteManyAsync(r => !excludingIdsList.Contains(r.Id), cancellationToken);
    }


    public async Task Upsert(Repository repository, CancellationToken cancellationToken)
    {
        // because we constantly refresh the database, we are looking to upsert the record here
        var filter = Builders<Repository>.Filter
            .Eq(r => r.Id, repository.Id);
        await Collection.ReplaceOneAsync(filter, repository, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    protected override List<CreateIndexModel<Repository>> DefineIndexes(IndexKeysDefinitionBuilder<Repository> builder)
    {
        var createdAtIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.CreatedAt));
        var teamsIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.Teams));
        var languageIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.PrimaryLanguage));
        var isTemplateIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.IsTemplate));
        var isArchivedIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.IsArchived));
        var teamIdIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.Teams.Select(t => t.TeamId)),
            new CreateIndexOptions { Sparse = true });

        return new List<CreateIndexModel<Repository>>
        {
            createdAtIndex,
            teamsIndex,
            languageIndex,
            isTemplateIndex,
            isArchivedIndex,
            teamIdIndex
        };
    }
}