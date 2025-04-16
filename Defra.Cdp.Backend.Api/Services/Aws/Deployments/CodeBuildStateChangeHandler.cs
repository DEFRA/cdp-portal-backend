using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Migrations;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class CodeBuildStateChangeHandler(IDatabaseMigrationService databaseMigrationService, ILogger<CodeBuildStateChangeHandler> logger)
{
    public async Task Handle(string id, CodeBuildStateChangeEvent codeBuildEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling migration update from codebuild for {BuildId}", codeBuildEvent.Detail.BuildId);

        var updated = await databaseMigrationService.UpdateStatus(codeBuildEvent.Detail.BuildId, codeBuildEvent.Detail.BuildStatus,
            codeBuildEvent.Time, cancellationToken);
        if (updated)
        {
            logger.LogInformation("Migration update from for {BuildId} updated to {Status}", codeBuildEvent.Detail.BuildId, codeBuildEvent.Detail.BuildStatus);
        }
        else
        {
            logger.LogWarning("Migration update from for {BuildId} updated to {Status} Failed, unable to find matching build id", codeBuildEvent.Detail.BuildId, codeBuildEvent.Detail.BuildStatus);
        }
    }

    public async Task Handle(string id, CodeBuildLambdaEvent lambdaEvent, CancellationToken cancellationToken)
    {
        
        var result = await databaseMigrationService.Link(lambdaEvent.CdpMigrationId, lambdaEvent.BuildId, cancellationToken);
        if (result == null)
        {
            logger.LogWarning("Failed to link migration {CdpMigrationId} to Build Id {BuildId}", lambdaEvent.CdpMigrationId, lambdaEvent.BuildId);    
        }
        else
        {
            logger.LogInformation("Linked migration {CdpMigrationId} to Build Id {BuildId}", lambdaEvent.CdpMigrationId,
                lambdaEvent.BuildId);
        }
    }
}