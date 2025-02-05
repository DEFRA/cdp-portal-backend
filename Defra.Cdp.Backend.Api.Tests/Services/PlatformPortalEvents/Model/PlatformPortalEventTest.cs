using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.PlatformEvents.Model;

namespace Defra.Cdp.Backend.Api.Tests.Services.PlatformEvents.Model;

public class PlatformEventTest
{
   [Fact]
   public void WillDeserializeDailyServiceCodeCostsServiceEvent()
   {
      const string messageBody = """
            {
               "eventType": "last-calendar-day-costs-by-service-code",
               "timestamp": "2024-11-23T15:10:10.123123+00:00",
               "payload": {
               "environment": "infra-dev",
               "cost_reports": [
                  {
                     "cost": 123.45,
                     "unit": "usd",
                     "date_from": "2025-01-09",
                     "date_to": "2025-01-10",
                     "serviceCode": "ABC",
                     "serviceName": "SQS"
                  }
               ]
               }
            }
            """;

      var workflowEvent = JsonSerializer.Deserialize<CommonEvent<ServiceCodeCostsPayload>>(messageBody);

      Assert.Equal("last-calendar-day-costs-by-service-code", workflowEvent?.EventType);
      Assert.Equal("infra-dev", workflowEvent?.Payload.Environment);
      Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);
      Assert.Equal((decimal)123.45, workflowEvent?.Payload.CostReports[0].Cost);
   }

   [Fact]
   public void WillDeserializeDailyTotalCostsServiceEvent()
   {
      const string messageBody = """
            {
               "eventType": "last-calendar-day-total-cost",
               "timestamp": "2024-11-23T15:10:10.123123+00:00",
               "payload": {
               "environment": "infra-dev",
               "cost_reports":
                  {
                     "cost": 3123.45,
                     "unit": "usd",
                     "date_from": "2025-01-09",
                     "date_to": "2025-01-10"
                  }
               }
            }
            """;

      var workflowEvent = JsonSerializer.Deserialize<CommonEvent<TotalCostsPayload>>(messageBody);

      Assert.Equal("last-calendar-day-total-cost", workflowEvent?.EventType);
      Assert.Equal("infra-dev", workflowEvent?.Payload.Environment);
      Assert.Equal(new DateTime(2024, 11, 23, 15, 10, 10, 123, 123), workflowEvent?.Timestamp);
      Assert.Equal((decimal)3123.45, workflowEvent?.Payload.CostReports.Cost);
   }

}
