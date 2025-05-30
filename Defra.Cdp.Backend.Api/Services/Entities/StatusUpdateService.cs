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
    IEntityStatusService entityStatusService,
    IOptions<GithubOptions> githubOptions)
    : IStatusUpdateService
{
    public async Task UpdateOverallStatus(string repositoryName, CancellationToken cancellationToken)
    {
        await entityStatusService.UpdateOverallStatus(repositoryName, cancellationToken);
        var currentStatus = await legacyStatusService.StatusForRepositoryName(repositoryName, cancellationToken);

        if (currentStatus == null)
        {
            return;
        }

        var overallStatus = StatusHelper.CalculateOverallStatus(githubOptions.Value.Repos, currentStatus);
        await legacyStatusService.UpdateStatus(overallStatus, repositoryName, cancellationToken);
    }
}