using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface IRepositoryService
{
    Task Upsert(Repository repository);

    Task UpsertMany(IEnumerable<Repository> repositories, CancellationToken cancellationToken);

    Task<List<Repository>> AllRepositories(bool excludeTemplates);

    Task<List<Repository>> FindRepositoriesByTeam(string team, bool excludeTemplates);

    Task<Repository?> FindRepositoryById(string id);
}

public class RepositoryService : MongoService<Repository>, IRepositoryService
{
    public RepositoryService(IMongoDbClientFactory connectionFactory,
        ILoggerFactory loggerFactory) : base(connectionFactory, "repositories", loggerFactory)
    {
    }


    public async Task Upsert(Repository repository)
    {
        // because we constantly refresh the database, we are looking to upsert the record here
        var filter = Builders<Repository>.Filter
            .Eq(r => r.Id, repository.Id);
        await Collection.ReplaceOneAsync(filter, repository, new ReplaceOptions { IsUpsert = true });
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
            });
        await Collection.BulkWriteAsync(replaceOneModels, new BulkWriteOptions(), cancellationToken);
    }

    public async Task<Repository?> FindRepositoryById(string id)
    {
        var repository =
            await Collection
                .Find(Builders<Repository>.Filter.Eq(r => r.Id, id))
                .FirstOrDefaultAsync();
        return repository;
    }

    public async Task<List<Repository>> AllRepositories(bool excludeTemplates)
    {
        var findDefinition = excludeTemplates
            ? Builders<Repository>.Filter.Eq(r => r.IsTemplate, false)
            : Builders<Repository>.Filter.Empty;

        var repositories =
            await Collection
                .Find(findDefinition)
                .SortBy(r => r.Id)
                .ToListAsync();
        return repositories;
    }

    public async Task<List<Repository>> FindRepositoriesByTeam(string team, bool excludeTemplates)
    {
        var baseFilter = Builders<Repository>.Filter.Eq(r => r.Teams, new[] { team });

        var findDefinition = excludeTemplates
            ? Builders<Repository>.Filter.And(baseFilter, Builders<Repository>.Filter.Eq(r => r.IsTemplate, false))
            : baseFilter;

        var repositories =
            await Collection
                .Find(findDefinition)
                .SortBy(r => r.Id)
                .ToListAsync();
        return repositories;
    }

    protected override List<CreateIndexModel<Repository>> DefineIndexes(IndexKeysDefinitionBuilder<Repository> builder)
    {
        var createdAtIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.CreatedAt));
        var teamsIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.Teams));
        var languageIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.PrimaryLanguage));
        var isTemplateIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.IsTemplate));
        var isArchivedIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.IsArchived));

        return new List<CreateIndexModel<Repository>>
        {
            createdAtIndex,
            teamsIndex,
            languageIndex,
            isTemplateIndex,
            isArchivedIndex
        };
    }
}