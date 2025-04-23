using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Entities.LegacyHelpers;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IStatusUpdateService
{
    Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken);
}

public class StatusUpdateService(
    ILegacyStatusService legacyStatusService,
    IEntitiesService entitiesService,
    IOptions<GithubOptions> githubOptions)
    : IStatusUpdateService
{
    public async Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken)
    {
        var currentStatus = await legacyStatusService.StatusForRepositoryName(repositoryName, cancellationToken);

        if (currentStatus == null)
        {
            return;
        }

        var overallStatus = StatusHelper.CalculateOverallStatus(githubOptions.Value.Repos, currentStatus);
        await entitiesService.UpdateStatus(overallStatus.ToEntityStatus(), repositoryName, cancellationToken);
        await legacyStatusService.UpdateStatus(overallStatus, repositoryName, cancellationToken);
    }
}