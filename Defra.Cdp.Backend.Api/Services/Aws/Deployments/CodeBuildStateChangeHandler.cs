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
        switch (result)
        {
            case LinkMigrationOutcome.LinkedOk:
                logger.LogInformation("Linked migration {CdpMigrationId} to Build Id {BuildId}", lambdaEvent.CdpMigrationId,
                    lambdaEvent.BuildId);
                return;
            case LinkMigrationOutcome.AlreadyLinked:
                logger.LogWarning("Migration {CdpMigrationId} is already linked to a different build id", lambdaEvent.CdpMigrationId);
                return;
            case LinkMigrationOutcome.UnknownMigrationId:
                if (lambdaEvent.Request != null && !string.IsNullOrEmpty(lambdaEvent.BuildId))
                {
                    logger.LogDebug("Unknown migration {CdpMigrationId} attempting to create from request",
                        lambdaEvent.CdpMigrationId);
                    var migration = DatabaseMigration.FromRequest(lambdaEvent.Request);
                    await databaseMigrationService.CreateMigration(migration, cancellationToken);
                    var createFromRequest = await databaseMigrationService.Link(migration.CdpMigrationId, lambdaEvent.BuildId, cancellationToken);
                    logger.LogInformation("Created migration {Id} -> {BuildId} from request: {Outcome}", migration.CdpMigrationId, migration.BuildId, createFromRequest.ToString());
                }
                else
                {
                    logger.LogWarning("Unknown migration {CdpMigrationId}", lambdaEvent.CdpMigrationId);
                }
                return;
            default:
                logger.LogWarning("Unknown Link Outcome status: {result}", result);
                return;
        }
    }
}