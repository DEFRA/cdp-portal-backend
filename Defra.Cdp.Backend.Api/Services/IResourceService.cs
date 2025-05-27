namespace Defra.Cdp.Backend.Api.Services;


public interface IResourceService
{
    public string ResourceName();
    public Task<Boolean> ExistsForRepositoryName(string repositoryName, CancellationToken cancellationToken);
}