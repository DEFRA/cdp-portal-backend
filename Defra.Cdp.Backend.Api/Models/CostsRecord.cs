using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Driver.Core.WireProtocol.Messages;
using Amazon.Util.Internal.PlatformServices;

namespace Defra.Cdp.Backend.Api.Models;

public enum CostReportType
{
   ServiceCode,
   Total
}

public enum CostReportEventType
{
   CostReport,
   TotalCostReport
}

public enum ReportTimeUnit
{
   Daily,
   ThirtyDays,
   Monthly
}

public static class CdpEnvironments
{
   public static string[] All => new[] { "infra-dev", "management", "dev", "test", "ext-test", "prod" };

   public static bool IsValid(string environment)
   {
      return All.Contains(environment);
   }
}

public record CostsRecordsByServiceCodeAndEnvironment(string ServiceCode, string Environment, List<ServiceCodeCostsRecord> CostsRecords)
{
   public CostsRecordsByServiceCodeAndEnvironment(string serviceCode, string environment) : this(serviceCode, environment, [])
   {

   }

   public CostsRecordsByServiceCodeAndEnvironment Add(ServiceCodeCostsRecord record)
   {
      CostsRecords.Add(record);
      return this;
   }

   public List<ServiceCodeCostsRecord> GetCosts()
   {
      return CostsRecords;
   }

};

public record EnvironmentsCostsByServiceCode(string ServiceCode, Dictionary<string, CostsRecordsByServiceCodeAndEnvironment> CostsRecords)
{
   public EnvironmentsCostsByServiceCode(string serviceCode) : this(serviceCode, [])
   {
   }

   public EnvironmentsCostsByServiceCode Add(string environment, CostsRecordsByServiceCodeAndEnvironment costsRecord)
   {
      CostsRecords.Add(environment, costsRecord);
      return this;
   }

   public CostsRecordsByServiceCodeAndEnvironment? GetCosts(string environment)
   {
      return CostsRecords.ContainsKey(environment) ? CostsRecords[environment] : null;
   }

   public List<string> ListEnvironments()
   {
      return CostsRecords.Select(x => x.Key).ToList();
   }

};

public record ServiceCodesCosts(Dictionary<string, EnvironmentsCostsByServiceCode> CostsRecords)
{
   public ServiceCodesCosts() : this([])
   {
   }

   public ServiceCodesCosts Add(string serviceCode, EnvironmentsCostsByServiceCode costsRecord)
   {
      CostsRecords.Add(serviceCode, costsRecord);
      return this;
   }

   public EnvironmentsCostsByServiceCode? GetCosts(string serviceCode)
   {
      return CostsRecords.ContainsKey(serviceCode) ? CostsRecords[serviceCode] : null;
   }

   public CostsRecordsByServiceCodeAndEnvironment? GetCosts(string serviceCode, string environment)
   {
      return CostsRecords.ContainsKey(serviceCode) ? CostsRecords[serviceCode].GetCosts(environment) : null;
   }

   public List<string> ListServiceCodes()
   {
      return CostsRecords.Select(x => x.Key).ToList();
   }
}

[BsonIgnoreExtraElements]
public record ServiceCodeCostsRecord(string EventType, DateTime EventTimestamp, string Environment, string ServiceCode, string AwsService, CostReport CostReport)
{

   public static ServiceCodeCostsRecord FromPayloads(string eventType, DateTime eventTimestamp, string environment, ServiceCodeCostReportPayload costReportPayload)
   {
      if (costReportPayload == null) throw new ArgumentNullException(nameof(costReportPayload));
      if (string.IsNullOrEmpty(costReportPayload.ServiceCode))
         throw new ArgumentException("ServiceCode cannot be null or empty", nameof(costReportPayload.ServiceCode));
      if (string.IsNullOrEmpty(costReportPayload.AwsService))
         throw new ArgumentException("AwsService cannot be null or empty", nameof(costReportPayload.AwsService));
      return new ServiceCodeCostsRecord(
         eventType,
         eventTimestamp,
         environment,
         costReportPayload.ServiceCode,
         costReportPayload.AwsService,
         CostReport.FromServiceCodeCostReportPayload(costReportPayload));
   }

   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;

   [BsonElement("createdAt")]
   [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
   public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public record TotalCostsRecord(string EventType, DateTime EventTimestamp, string Environment, CostReport CostReport)
{

   public static TotalCostsRecord FromPayloads(string eventType, DateTime eventTimestamp, string environment, TotalCostReportPayload costReportPayload)
   {
      if (costReportPayload == null) throw new ArgumentNullException(nameof(costReportPayload));
      return new TotalCostsRecord(
         eventType,
         eventTimestamp,
         environment,
         CostReport.FromTotalCostReportPayload(costReportPayload));
   }

   [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
   [BsonIgnoreIfDefault]
   [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
   public ObjectId? Id { get; init; } = default!;

   [BsonElement("createdAt")]
   [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
   public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
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
