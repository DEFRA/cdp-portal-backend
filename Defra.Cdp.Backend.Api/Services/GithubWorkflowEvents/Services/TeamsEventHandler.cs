using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.Teams;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface ITeamsEventHandler : IGithubWorkflowEventHandler;

public class TeamsEventHandler(ITeamsService teamsService, IUserServiceBackendClient usbClient, ILogger<TeamsEventHandler> logger) : ITeamsEventHandler
{

    public async Task Handle(string messageBody, CancellationToken cancellationToken)
    {
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<TeamsPayload>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogWarning("Failed to parse Github workflow event - message: {MessageBody}", messageBody);
            return;
        }
        var teams = workflowEvent.Payload.Teams.Select(t => t.ToTeam()).ToList();
        await teamsService.SyncTeams(teams, cancellationToken);

        var usbTeams = workflowEvent.Payload.Teams.Select(t => t.ToUserServiceTeamSync()).ToList();
        await usbClient.SyncTeams(usbTeams, cancellationToken);
    }

    public string EventType => "team";
}