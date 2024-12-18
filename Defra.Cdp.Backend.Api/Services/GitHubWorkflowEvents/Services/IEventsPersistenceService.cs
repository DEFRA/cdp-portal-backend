using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

public interface IEventsPersistenceService<T>
{
    Task PersistEvent(Event<T> workflowEvent, CancellationToken cancellationToken);
}