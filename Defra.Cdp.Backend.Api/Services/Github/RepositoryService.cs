using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface IRepositoryService
{
    Task UpsertMany(IEnumerable<Repository> repositories, CancellationToken cancellationToken);

    Task DeleteUnknownRepos(IEnumerable<string> knownReposIds, CancellationToken cancellationToken);

    Task<Repository?> FindRepositoryById(string id, CancellationToken cancellationToken);
}

public class RepositoryService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<Repository>(connectionFactory, "repositories", loggerFactory), IRepositoryService
{
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

        if (replaceOneModels.Count > 0)
            // BulkWrite fails if it's called with a zero length array
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

    public async Task DeleteUnknownRepos(IEnumerable<string> knownReposIds, CancellationToken cancellationToken)
    {
        var excludingIdsList = knownReposIds.ToList();
        if (excludingIdsList.Count == 0) throw new ArgumentException("excluded repositories cannot be empty");
        await Collection.DeleteManyAsync(r => !excludingIdsList.Contains(r.Id), cancellationToken);
    }

    protected override List<CreateIndexModel<Repository>> DefineIndexes(IndexKeysDefinitionBuilder<Repository> builder)
    {
        var createdAtIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.CreatedAt));
        var languageIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.PrimaryLanguage));
        var isArchivedIndex = new CreateIndexModel<Repository>(builder.Ascending(r => r.IsArchived));

        return
        [
            createdAtIndex,
            languageIndex,
            isArchivedIndex
        ];
    }
}