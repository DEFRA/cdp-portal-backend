using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ServiceCodesCostsResponse
{
   public ServiceCodesCostsResponse(ServiceCodesCosts costsRecords)
   {
      // timeUnit = nameof(costsRecords.timeUnit);
      TimeUnit = costsRecords.timeUnit.ToString();
      DateFrom = costsRecords.dateFrom;
      DateTo = costsRecords.dateTo;
      CostReports = costsRecords.CostsRecords.Select(costs => new CostReportResponse(costs)).ToList();
      ByServiceCode = costsRecords.GetCostsByServiceCodes().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(costs => new CostReportResponse(costs)).ToList());
      ByEnvironment = costsRecords.GetCostsByEnvironments().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(costs => new CostReportResponse(costs)).ToList());
   }

   [JsonPropertyName("timeUnit")] public string TimeUnit { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }
   [JsonPropertyName("costReports")] public List<CostReportResponse> CostReports { get; }

   [JsonPropertyName("byEnvironment")] public Dictionary<string, List<CostReportResponse>> ByEnvironment { get; }
   [JsonPropertyName("byServiceCode")] public Dictionary<string, List<CostReportResponse>> ByServiceCode { get; }
}


public class CostReportResponse
{
   public CostReportResponse(ServiceCodeCostsRecord CostsRecord)
   {
      Environment = CostsRecord.Environment;
      ServiceCode = CostsRecord.ServiceCode;
      AwsService = CostsRecord.AwsService;
      Cost = CostsRecord.CostReport.Cost;
      Currency = CostsRecord.CostReport.Currency;
      DateFrom = CostsRecord.CostReport.DateFrom;
      DateTo = CostsRecord.CostReport.DateTo;
      EventType = CostsRecord.EventType;
      EventTimestamp = CostsRecord.EventTimestamp;
   }

   [JsonPropertyName("environment")] public string Environment { get; }
   [JsonPropertyName("serviceCode")] public string ServiceCode { get; }
   [JsonPropertyName("awsService")] public string AwsService { get; }
   [JsonPropertyName("eventType")] public string EventType { get; }
   [JsonPropertyName("eventTimestamp")] public DateTime EventTimestamp { get; }
   [JsonPropertyName("cost")] public decimal Cost { get; }
   [JsonPropertyName("currency")] public string Currency { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }

}
