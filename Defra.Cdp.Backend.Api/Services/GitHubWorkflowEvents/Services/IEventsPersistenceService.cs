using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;

public interface IEventsPersistenceService<T>
{
   Task PersistEvent(CommonEvent<T> workflowEvent, CancellationToken cancellationToken);
}