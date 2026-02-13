using Defra.Cdp.Backend.Api.Services.TenantArtifacts;

namespace Defra.Cdp.Backend.Api.Services.Dependencies;

public interface ISbomEcrEventHandler
{
    public Task Handle(CancellationToken ct);
}

public class SbomEcrEventHandler(ISbomExplorerClient client, IDeployableArtifactsService artifactsService, ILogger<SbomEcrEventHandler> logger) : ISbomEcrEventHandler
{
    public async Task Handle(CancellationToken ct)
    {
        try
        {
            var latest = await artifactsService.FindLatestForAll(ct);
            await client.PushLatestVersions(latest, ct);
            logger.LogWarning("Pushed {Size} latest artifacts for to cdp-sbom-explorer-backend", latest.Count);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to push update to cdp-sbom-explorer-backend");
        }
    }
}