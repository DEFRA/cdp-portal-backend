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
      CostReports = costsRecords.CostsRecords.Select(costs => new ServiceCodeCostReportResponse(costs)).ToList();
      ByServiceCode = costsRecords.GetCostsByServiceCodes().ToDictionary(kvp => kvp.Key, kvp =>
        kvp.Value.Select(costs => new ServiceCodeCostReportResponse(costs)).ToList());
      ByEnvironment = costsRecords.GetCostsByEnvironments().ToDictionary(kvp => kvp.Key, kvp =>
        kvp.Value.Select(costs => new ServiceCodeCostReportResponse(costs)).ToList());
      ByDateFrom = costsRecords.GetCostsByDateFrom().ToDictionary(kvp => kvp.Key, kvp =>
        kvp.Value.Select(costs => new ServiceCodeCostReportResponse(costs)).ToList());
   }

   [JsonPropertyName("timeUnit")] public string TimeUnit { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }
   [JsonPropertyName("costReports")] public List<ServiceCodeCostReportResponse> CostReports { get; }

   [JsonPropertyName("byEnvironment")] public Dictionary<string, List<ServiceCodeCostReportResponse>> ByEnvironment { get; }
   [JsonPropertyName("byServiceCode")] public Dictionary<string, List<ServiceCodeCostReportResponse>> ByServiceCode { get; }
   [JsonPropertyName("byDateFrom")] public Dictionary<DateOnly, List<ServiceCodeCostReportResponse>> ByDateFrom { get; }
}


public class ServiceCodeCostReportResponse
{
   public ServiceCodeCostReportResponse(ServiceCodeCostsRecord CostsRecord)
   {
      Environment = CostsRecord.Environment;
      EventType = CostsRecord.EventType;
      EventTimestamp = CostsRecord.EventTimestamp;
      ServiceCode = CostsRecord.ServiceCode;
      AwsService = CostsRecord.AwsService;
      CostReport = new CostReportResponse(CostsRecord.CostReport);
   }

   [JsonPropertyName("environment")] public string Environment { get; }
   [JsonPropertyName("eventType")] public string EventType { get; }
   [JsonPropertyName("eventTimestamp")] public DateTime EventTimestamp { get; }
   [JsonPropertyName("serviceCode")] public string ServiceCode { get; }
   [JsonPropertyName("awsService")] public string AwsService { get; }
   [JsonPropertyName("costReport")] public CostReportResponse CostReport { get; }
}

public class CostReportResponse
{
   public CostReportResponse(CostReport CostReport)
   {
      Cost = CostReport.Cost;
      Currency = CostReport.Currency;
      DateFrom = CostReport.DateFrom;
      DateTo = CostReport.DateTo;
   }

   [JsonPropertyName("cost")] public decimal Cost { get; }
   [JsonPropertyName("currency")] public string Currency { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }

}

public class TotalCostReportResponse
{
   public TotalCostReportResponse(TotalCostsRecord CostsRecord)
   {
      Environment = CostsRecord.Environment;
      EventType = CostsRecord.EventType;
      EventTimestamp = CostsRecord.EventTimestamp;
      CostReport = new CostReportResponse(CostsRecord.CostReport);
   }

   [JsonPropertyName("environment")] public string Environment { get; }
   [JsonPropertyName("eventType")] public string EventType { get; }
   [JsonPropertyName("eventTimestamp")] public DateTime EventTimestamp { get; }
   [JsonPropertyName("costReport")] public CostReportResponse CostReport { get; }
}


public class TotalCostsResponse
{
   public TotalCostsResponse(TotalCosts costsRecords)
   {
      TimeUnit = costsRecords.timeUnit.ToString();
      DateFrom = costsRecords.dateFrom;
      DateTo = costsRecords.dateTo;
      CostReports = costsRecords.CostsRecords.Select(costs => new TotalCostReportResponse(costs)).ToList();
      ByEnvironment = costsRecords.GetCostsByEnvironments().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(costs => new TotalCostReportResponse(costs)).ToList());
      ByDateFrom = costsRecords.GetCostsByDateFrom().ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Select(costs => new TotalCostReportResponse(costs)).ToList());
   }

   [JsonPropertyName("timeUnit")] public string TimeUnit { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }
   [JsonPropertyName("costReports")] public List<TotalCostReportResponse> CostReports { get; }
   [JsonPropertyName("byEnvironment")] public Dictionary<string, List<TotalCostReportResponse>> ByEnvironment { get; }
   [JsonPropertyName("byDateFrom")] public Dictionary<DateOnly, List<TotalCostReportResponse>> ByDateFrom { get; }
}
