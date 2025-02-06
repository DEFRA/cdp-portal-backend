using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.PlatformEvents;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Model;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Services;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.PlatformEvents;

public class PlatformEventHandlerTest
{
   private readonly IServiceCodeCostsService serviceCodeCostsService = Substitute.For<IServiceCodeCostsService>();
   private readonly ITotalCostsService totalCostsService = Substitute.For<ITotalCostsService>();

   private PlatformEventHandler createHandler()
   {
      return new PlatformEventHandler(
          serviceCodeCostsService,
          totalCostsService,
          ConsoleLogger.CreateLogger<PlatformEventHandler>());
   }


   [Fact]
   public async Task WillProcessServiceCodeCostsServicesEvent()
   {
      var eventHandler = createHandler();

      var eventType = new CommonEventWrapper { EventType = "last-calendar-day-costs-by-service-code" };
      const string messageBody = """
                                      {
                                         "eventType": "last-calendar-day-costs-by-service-code",
                                         "timestamp": "2024-10-23T15:10:10.123",
                                         "payload": {
                                            "environment": "prod",
                                            "cost_reports": [
                                               {
                                               "serviceCode": "CDP",
                                               "serviceName": "SQS",
                                               "cost": 123.45,
                                               "unit": "usd",
                                               "date_from": "2025-01-09",
                                               "date_to": "2025-01-10"
                                               },
                                               {
                                               "serviceCode": "ABC",
                                               "serviceName": "SQS",
                                               "cost": 45.45,
                                               "unit": "usd",
                                               "date_from": "2025-01-09",
                                               "date_to": "2025-01-10"
                                               }
                                            ]
                                         }
                                      }
                                   """;

      await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

      await serviceCodeCostsService.Received(1).PersistEvent(
          Arg.Is<CommonEvent<ServiceCodeCostsPayload>>(e =>
              e.EventType == "last-calendar-day-costs-by-service-code" && e.Payload.Environment == "prod" &&
              e.Payload.CostReports.Count == 2),
          Arg.Any<CancellationToken>());
      await totalCostsService.DidNotReceive()
          .PersistEvent(Arg.Any<CommonEvent<TotalCostsPayload>>(), Arg.Any<CancellationToken>());
   }

   [Fact]
   public async Task WillProcessTotalCostsServicesEvent()
   {
      var eventHandler = createHandler();

      var eventType = new CommonEventWrapper { EventType = "last-calendar-month-total-cost" };
      const string messageBody = """
                                      {
                                         "eventType": "last-calendar-month-total-cost",
                                         "timestamp": "2024-10-23T15:10:10.123",
                                         "payload": {
                                            "environment": "prod",
                                            "cost_reports":
                                               {
                                               "cost": 123.45,
                                               "unit": "usd",
                                               "date_from": "2025-01-09",
                                               "date_to": "2025-01-10"
                                               }
                                         }
                                      }
                                   """;

      await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

      await totalCostsService.Received(1).PersistEvent(
          Arg.Is<CommonEvent<TotalCostsPayload>>(e =>
              e.EventType == "last-calendar-month-total-cost" && e.Payload.Environment == "prod" &&
              e.Payload.CostReports.Unit == "usd"),
          Arg.Any<CancellationToken>());
      await serviceCodeCostsService.DidNotReceive()
          .PersistEvent(Arg.Any<CommonEvent<ServiceCodeCostsPayload>>(), Arg.Any<CancellationToken>());
   }

   [Fact]
   public async Task UnrecognizedPlatformEvent()
   {
      var eventHandler = createHandler();

      var eventType = new CommonEventWrapper { EventType = "unrecognized-platform-portal-event" };
      var messageBody =
          """
            { "eventType": "unrecognized-platform-portal-event",
              "timestamp": "2024-10-23T15:10:10.123",
              "payload": {
                "environment": "test"
              }
            }
            """;

      await eventHandler.Handle(eventType, messageBody, CancellationToken.None);

      await totalCostsService.DidNotReceive()
          .PersistEvent(Arg.Any<CommonEvent<TotalCostsPayload>>(), Arg.Any<CancellationToken>());
      await serviceCodeCostsService.DidNotReceive()
          .PersistEvent(Arg.Any<CommonEvent<ServiceCodeCostsPayload>>(), Arg.Any<CancellationToken>());
   }
}
