using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Driver.Core.WireProtocol.Messages;

namespace Defra.Cdp.Backend.Api.Models;

[BsonIgnoreExtraElements]
public record ServiceCodeCostsRecord(string EventType, DateTime ReportTimestamp, string Environment, string ServiceCode, string AwsService, CostReport CostReport)
{

   public static ServiceCodeCostsRecord FromPayloads(string eventType, DateTime reportTimestamp, string environment, ServiceCodeCostReportPayload costReportPayload)
   {
      if (costReportPayload == null) throw new ArgumentNullException(nameof(costReportPayload));
      if (string.IsNullOrEmpty(costReportPayload.ServiceCode))
         throw new ArgumentException("ServiceCode cannot be null or empty", nameof(costReportPayload.ServiceCode));
      if (string.IsNullOrEmpty(costReportPayload.AwsService))
         throw new ArgumentException("AwsService cannot be null or empty", nameof(costReportPayload.AwsService));
      return new ServiceCodeCostsRecord(
         eventType,
         reportTimestamp,
         environment,
         costReportPayload.ServiceCode,
         costReportPayload.AwsService,
         CostReport.FromServiceCodeCostReportPayload(costReportPayload));
   }

   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;

}

[BsonIgnoreExtraElements]
public record TotalCostsRecord(string EventType, DateTime ReportTimestamp, string Environment, CostReport CostReport)
{

   public static TotalCostsRecord FromPayloads(string eventType, DateTime reportTimestamp, string environment, TotalCostReportPayload costReportPayload)
   {
      if (costReportPayload == null) throw new ArgumentNullException(nameof(costReportPayload));
      return new TotalCostsRecord(
         eventType,
         reportTimestamp,
         environment,
         CostReport.FromTotalCostReportPayload(costReportPayload));
   }

   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;

}

public record CostReport(decimal Cost, string Currency, DateOnly DateFrom, DateOnly DateTo)
{
   public static CostReport FromServiceCodeCostReportPayload(ServiceCodeCostReportPayload payload)
   {
      return new CostReport(
         payload.Cost,
         payload.Unit,
         payload.DateFrom,
         payload.DateTo);
   }
   public static CostReport FromTotalCostReportPayload(TotalCostReportPayload payload)
   {
      return new CostReport(
         payload.Cost,
         payload.Unit,
         payload.DateFrom,
         payload.DateTo);
   }
}
