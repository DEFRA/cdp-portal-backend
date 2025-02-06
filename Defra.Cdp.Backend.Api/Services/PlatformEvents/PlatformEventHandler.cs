using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Model;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Services;

namespace Defra.Cdp.Backend.Api.Services.PlatformEvents;

public interface IPlatformEventHandler
{
   Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken);
}

/**
 * Handles payloads for platform portal
 * All messages have the same outer body detailing the source & action.
 */
public class PlatformEventHandler(
    IServiceCodeCostsService serviceCodeCostsService,
    ITotalCostsService totalCostsService,
    ILogger<PlatformEventHandler> logger)
    : IPlatformEventHandler
{
   public async Task Handle(CommonEventWrapper eventWrapper, string messageBody, CancellationToken cancellationToken)
   {
      switch (eventWrapper.EventType)
      {
         case "last-calendar-day-costs-by-service-code":
         case "last-calendar-month-costs-by-service-code":
         case "last-30-days-costs-by-service-code":
            await HandleEvent(eventWrapper, messageBody, serviceCodeCostsService, cancellationToken);
            break;
         case "last-calendar-day-total-cost":
         case "last-calendar-month-total-cost":
         case "last-30-days-total-cost":
            await HandleEvent(eventWrapper, messageBody, totalCostsService, cancellationToken);
            break;
         default:
            logger.LogInformation("Ignoring event: {EventType} not handled {Message}", eventWrapper.EventType, messageBody);
            break;
      }
   }

   private async Task HandleEvent<T>(CommonEventWrapper eventWrapper, string messageBody, IEventsPersistenceService<T> service,
       CancellationToken cancellationToken)
   {
      logger.LogInformation("Handling event: {EventType}", eventWrapper.EventType);
      var workflowEvent = JsonSerializer.Deserialize<CommonEvent<T>>(messageBody);
      if (workflowEvent == null)
      {
         logger.LogInformation("Failed to parse Platform Portal event - message: {MessageBody}", messageBody);
         return;
      }

      await service.PersistEvent(workflowEvent, cancellationToken);
   }
}
