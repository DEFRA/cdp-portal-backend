using System.Dynamic;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface IRepositoryService
{
    Task Upsert(Repository repository, CancellationToken cancellationToken);

    Task UpsertMany(IEnumerable<Repository> repositories, CancellationToken cancellationToken);

    Task DeleteUnknownRepos(IEnumerable<string> knownReposIds, CancellationToken cancellationToken);

    Task<List<Repository>> AllRepositories(bool excludeTemplates, CancellationToken cancellationToken);

    Task<List<Repository>> FindRepositoriesByGitHubTeam(string team, bool excludeTemplates,
        CancellationToken cancellationToken);

    Task<List<Repository>> FindRepositoriesByTeamId(string id, bool excludeTemplates,
        CancellationToken cancellationToken);

    Task<List<Repository>> FindTeamRepositoriesByTopic(string teamId, CdpTopic topic,
        CancellationToken cancellationToken);

    Task<List<Repository>> FindRepositoriesByTopic(CdpTopic topic, CancellationToken cancellationToken);

    Task<Repository?> FindRepositoryWithTopicById(CdpTopic topic, string id, CancellationToken cancellationToken);

    Task<Repository?> FindRepositoryById(string id, CancellationToken cancellationToken);

    Task<ILookup<string, List<RepositoryTeam>>> TeamsLookup(CancellationToken cancellationToken);
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
            // BulkWrite fails if its called with a zero length array
            await Collection.BulkWriteAsync(replaceOneModels, new BulkWriteOptions(), cancellationToken);
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
        var builder = Builders<Repository>.Filter;
        var filter = builder.Empty;
        var withoutTemplatesFilter = builder.Eq(r => r.IsTemplate, false);
        var withCdpTopicFilter = builder.Where(r => r.Topics.Contains("cdp"));

        var findDefinition = excludeTemplates
            ? withoutTemplatesFilter
            : filter;

        var repositories =
            await Collection
                .Find(findDefinition & withCdpTopicFilter)
                .SortBy(r => r.Id)
                .ToListAsync(cancellationToken);
        return repositories;
    }

    public async Task<List<Repository>> FindRepositoriesByGitHubTeam(string team, bool excludeTemplates,
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

    public async Task<List<Repository>> FindRepositoriesByTeamId(string id, bool excludeTemplates,
        CancellationToken cancellationToken)
    {
        var baseFilter = Builders<Repository>.Filter.ElemMatch(r => r.Teams, t => t.TeamId == id);

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
        if (excludingIdsList.Count == 0) throw new ArgumentException("excluded repositories cannot be empty");
        await Collection.DeleteManyAsync(r => !excludingIdsList.Contains(r.Id), cancellationToken);
    }


    public async Task Upsert(Repository repository, CancellationToken cancellationToken)
    {
        // because we constantly refresh the database, we are looking to upsert the record here
        var filter = Builders<Repository>.Filter
            .Eq(r => r.Id, repository.Id);
        await Collection.ReplaceOneAsync(filter, repository, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<List<Repository>> FindTeamRepositoriesByTopic(string teamId, CdpTopic topic,
        CancellationToken cancellationToken)
    {
        var topicFilter = Builders<Repository>.Filter.All(r => r.Topics, ProvideCdpTopics(topic));
        var teamFilter = Builders<Repository>.Filter.ElemMatch(r => r.Teams, t => t.TeamId == teamId);

        var repositories =
            await Collection
                .Find(teamFilter & topicFilter)
                .SortBy(r => r.Id)
                .ToListAsync(cancellationToken);
        return repositories;
    }

    public async Task<Repository?> FindRepositoryWithTopicById(CdpTopic topic, string id,
        CancellationToken cancellationToken)
    {
        var topicFilter = Builders<Repository>.Filter.All(r => r.Topics, ProvideCdpTopics(topic));
        var repositoryFilter = Builders<Repository>.Filter.Eq(r => r.Id, id);

        var repository =
            await Collection
                .Find(repositoryFilter & topicFilter)
                .FirstOrDefaultAsync(cancellationToken);
        return repository;
    }

    public async Task<List<Repository>> FindRepositoriesByTopic(CdpTopic topic, CancellationToken cancellationToken)
    {
        return await Collection
            .Find(Builders<Repository>.Filter.All(r => r.Topics, ProvideCdpTopics(topic)))
            .SortBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    private record ServiceAndTeams(string Service, IEnumerable<RepositoryTeam> Teams);

    public async Task<ILookup<string, List<RepositoryTeam>>> TeamsLookup(CancellationToken cancellationToken)
    {
        var filter = Builders<Repository>.Filter.Empty;
        var results = await Collection.Find(filter).ToCursorAsync(cancellationToken);
        return results.ToEnumerable(cancellationToken).ToLookup(r => r.Id, r => r.Teams.ToList());
    }

    private List<string> ProvideCdpTopics(CdpTopic topic)
    {
        return new List<CdpTopic> { CdpTopic.Cdp, topic }.ConvertAll<string>(t => t.ToString().ToLower());
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

        return
        [
            createdAtIndex,
            teamsIndex,
            languageIndex,
            isTemplateIndex,
            isArchivedIndex,
            teamIdIndex
        ];
    }
}