namespace Defra.Cdp.Backend.Api.Services;


public interface IResourceService
{
    public string ResourceName();
    public Task<bool> ExistsForRepositoryName(string repositoryName, CancellationToken cancellationToken);
}