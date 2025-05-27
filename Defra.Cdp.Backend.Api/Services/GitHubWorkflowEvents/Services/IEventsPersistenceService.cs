using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IEventsPersistenceService<T>
{
    Task PersistEvent(CommonEvent<T> workflowEvent, CancellationToken cancellationToken);
}