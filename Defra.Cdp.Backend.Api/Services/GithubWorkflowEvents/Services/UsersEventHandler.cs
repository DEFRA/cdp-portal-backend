using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.Users;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IUsersEventHandler : IGithubWorkflowEventHandler;

public class UsersEventHandler(IUsersService usersService, ILogger<IUsersEventHandler> logger) : IUsersEventHandler
{
    public string EventType => "users";
    
    public async Task Handle(string messageBody, CancellationToken cancellationToken)
    {
        var workflowEvent = JsonSerializer.Deserialize<CommonEvent<UsersPayload>>(messageBody);
        if (workflowEvent == null)
        {
            logger.LogError("Failed to parse Github workflow event - message: {MessageBody}", messageBody);
            return;
        }
        var users = workflowEvent.Payload.Users.Select(t => t.ToUser()).ToList();
        await usersService.SyncUsers(users, cancellationToken);
    }
}