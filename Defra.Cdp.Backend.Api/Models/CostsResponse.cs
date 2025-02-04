using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ServiceCodesCostsResponse
{
   public ServiceCodesCostsResponse(ServiceCodesCosts costsRecords)
   {
      TimeUnit = costsRecords.timeUnit.ToString();
      DateFrom = costsRecords.dateFrom;
      DateTo = costsRecords.dateTo;
      CostReports = costsRecords.CostsRecords.Select(costs => new ServiceCodeCostReportResponse(costs)).ToList();
      ByServiceCode = costsRecords.GetCostsByServiceCodes().ToDictionary(r => r.Key, r => new ServiceCodeSummaryCostsResponse(r.Value, costsRecords.dateFrom, costsRecords.dateTo));
      ByEnvironment = costsRecords.GetCostsByEnvironments().ToDictionary(r => r.Key, r => new ServiceCodeSummaryCostsResponse(r.Value, costsRecords.dateFrom, costsRecords.dateTo));
      ByDateFrom = costsRecords.GetCostsByDateFrom().ToDictionary(r => r.Key, r => new ServiceCodeSummaryCostsResponse(r.Value, costsRecords.dateFrom, costsRecords.dateTo));
      Summarised = new CostReportResponse(new CostReport(costsRecords.SummarisedCost(), "USD", costsRecords.dateFrom, costsRecords.dateTo));
   }

   [JsonPropertyName("timeUnit")] public string TimeUnit { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }
   [JsonPropertyName("costReports")] public List<ServiceCodeCostReportResponse> CostReports { get; }
   [JsonPropertyName("summarised")] public CostReportResponse Summarised { get; }
   [JsonPropertyName("byEnvironment")] public Dictionary<string, ServiceCodeSummaryCostsResponse> ByEnvironment { get; }
   [JsonPropertyName("byServiceCode")] public Dictionary<string, ServiceCodeSummaryCostsResponse> ByServiceCode { get; }
   [JsonPropertyName("byDateFrom")] public Dictionary<DateOnly, ServiceCodeSummaryCostsResponse> ByDateFrom { get; }
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

public class ServiceCodeSummaryCostsResponse
{

   public ServiceCodeSummaryCostsResponse(List<ServiceCodeCostsRecord> costsRecords, DateOnly DateFrom, DateOnly DateTo)
   {
      CostReports = costsRecords.Select(costs => new ServiceCodeCostReportResponse(costs)).ToList();
      var allCosts = costsRecords.Select(costs => costs.CostReport.Cost).Sum();
      Summarised = new CostReportResponse(new CostReport(allCosts, "USD", DateFrom, DateTo));
   }

   [JsonPropertyName("summarised")] public CostReportResponse Summarised { get; }

   [JsonPropertyName("costReports")] public List<ServiceCodeCostReportResponse> CostReports { get; }

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
      ByEnvironment = costsRecords.GetCostsByEnvironments().ToDictionary(r => r.Key, r => new TotalSummaryCostsResponse(r.Value, costsRecords.dateFrom, costsRecords.dateTo));
      ByDateFrom = costsRecords.GetCostsByDateFrom().ToDictionary(r => r.Key, r => new TotalSummaryCostsResponse(r.Value, costsRecords.dateFrom, costsRecords.dateTo));
      Summarised = new CostReportResponse(new CostReport(costsRecords.SummarisedCost(), "USD", costsRecords.dateFrom, costsRecords.dateTo));
   }

   [JsonPropertyName("timeUnit")] public string TimeUnit { get; }
   [JsonPropertyName("dateFrom")] public DateOnly DateFrom { get; }
   [JsonPropertyName("dateTo")] public DateOnly DateTo { get; }
   [JsonPropertyName("costReports")] public List<TotalCostReportResponse> CostReports { get; }
   [JsonPropertyName("summarised")] public CostReportResponse Summarised { get; }
   [JsonPropertyName("byEnvironment")] public Dictionary<string, TotalSummaryCostsResponse> ByEnvironment { get; }
   [JsonPropertyName("byDateFrom")] public Dictionary<DateOnly, TotalSummaryCostsResponse> ByDateFrom { get; }
}

public class TotalSummaryCostsResponse
{

   public TotalSummaryCostsResponse(List<TotalCostsRecord> costsRecords, DateOnly DateFrom, DateOnly DateTo)
   {
      CostReports = costsRecords.Select(costs => new TotalCostReportResponse(costs)).ToList();
      var allCosts = costsRecords.Select(costs => costs.CostReport.Cost).Sum();
      Summarised = new CostReportResponse(new CostReport(allCosts, "USD", DateFrom, DateTo));
   }

   [JsonPropertyName("summarised")] public CostReportResponse Summarised { get; }

   [JsonPropertyName("costReports")] public List<TotalCostReportResponse> CostReports { get; }

}
