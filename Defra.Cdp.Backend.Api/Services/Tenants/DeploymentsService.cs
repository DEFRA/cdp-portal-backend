using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Repositories.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface IDeploymentsService
{
    Task<List<Deployment>> FindLatest(int offset = 0);
    public Task<List<Deployment>> FindLatest(string environment, int offset = 0);
    public Task<List<Deployment>> FindWhatsRunningWhere();
    public Task<List<Deployment>> FindWhatsRunningWhere(string serviceName);
    public Task<Deployment> FindDeployment(string deploymentId);

    public Task Insert(Deployment deployment);
}

public class DeploymentsService : IDeploymentsService
{
    private readonly IMongoCollection<Deployment> _deployments;

    public DeploymentsService(IMongoDbClientFactory connectionFactory)
    {
        _deployments = connectionFactory.GetCollection<Deployment>("deployments");
        EnsureIndexes();
    }

    public async Task<List<Deployment>> FindLatest(int offset = 0)
    {
        return await _deployments
            .Find(FilterDefinition<Deployment>.Empty)
            .Skip(offset)
            .Limit(200)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .ToListAsync();
    }

    public async Task<List<Deployment>> FindLatest(string environment, int offset = 0)
    {
        return await _deployments
            .Find(d => d.Environment == environment)
            .Skip(offset)
            .Limit(200)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .ToListAsync();
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere()
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await _deployments.Aggregate(pipeline).ToListAsync();
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(string serviceName)
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(d => d.Service == serviceName)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await _deployments.Aggregate(pipeline).ToListAsync();
    }

    public Task<Deployment> FindDeployment(string deploymentId)
    {
        return _deployments.Find(d => d.DeploymentId == deploymentId)
            .Limit(1)
            .FirstAsync();
    }

    public async Task Insert(Deployment deployment)
    {
        await _deployments.InsertOneAsync(deployment);
    }


    private IEnumerable<string?> EnsureIndexes()
    {
        var builder = Builders<Deployment>.IndexKeys;
        var indexModel = new CreateIndexModel<Deployment>(builder.Combine(builder.Ascending(r => r.Environment),
            builder.Ascending(r => r.Service)));
        var titleModel = new CreateIndexModel<Deployment>(builder.Descending(r => r.DeployedAt));
        var result = _deployments.Indexes.CreateMany(new[] { indexModel, titleModel });
        return result;
    }
}