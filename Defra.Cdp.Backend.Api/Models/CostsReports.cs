using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Driver.Core.WireProtocol.Messages;
using Amazon.Util.Internal.PlatformServices;

namespace Defra.Cdp.Backend.Api.Models;

public enum CostReportType
{
   ServiceCode,
   Total
}

public enum ReportTimeUnit
{
   Daily,
   ThirtyDays,
   Monthly
}

public static class ReportTimeUnits
{
   public static ReportTimeUnit ToTimeUnit(string timeUnit)
   {
      return timeUnit switch
      {
         "day" => ReportTimeUnit.Daily,
         "30day" => ReportTimeUnit.ThirtyDays,
         "month" => ReportTimeUnit.Monthly,
         _ => throw new ArgumentOutOfRangeException(nameof(timeUnit), timeUnit, null)
      };
   }
}

public record ServiceCodesCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, List<ServiceCodeCostsRecord> CostsRecords)
{

   public List<ServiceCodeCostsRecord> GetCosts()
   {
      return CostsRecords;
   }

   public List<ServiceCodeCostsRecord> GetCostsByServiceCode(string serviceCode)
   {
      return CostsRecords.Where(x => x.ServiceCode == serviceCode).ToList();
   }

   public List<ServiceCodeCostsRecord> GetCostsByEnvironment(string environment)
   {
      return CostsRecords.Where(x => x.Environment == environment).ToList();
   }

   public Dictionary<String, List<ServiceCodeCostsRecord>> GetCostsByServiceCodes()
   {
      return CostsRecords.GroupBy(r => r.ServiceCode).ToDictionary(g => g.Key, g => g.ToList());
   }

   public Dictionary<String, List<ServiceCodeCostsRecord>> GetCostsByEnvironments()
   {
      return CostsRecords.GroupBy(r => r.Environment).ToDictionary(g => g.Key, g => g.ToList());
   }

   public Dictionary<DateOnly, List<ServiceCodeCostsRecord>> GetCostsByDateFrom()
   {
      return CostsRecords.GroupBy(r => r.CostReport.DateFrom).ToDictionary(g => g.Key, g => g.ToList());
   }

   public decimal SummarisedCost()
   {
      return CostsRecords.Select(costs => costs.CostReport.Cost).Sum();
   }
}


public record TotalCosts(ReportTimeUnit timeUnit, DateOnly dateFrom, DateOnly dateTo, List<TotalCostsRecord> CostsRecords)
{

   public List<TotalCostsRecord> GetCosts()
   {
      return CostsRecords;
   }

   public List<TotalCostsRecord> GetCostsByEnvironment(string environment)
   {
      return CostsRecords.Where(x => x.Environment == environment).ToList();
   }

   public Dictionary<String, List<TotalCostsRecord>> GetCostsByEnvironments()
   {
      return CostsRecords.GroupBy(r => r.Environment).ToDictionary(g => g.Key, g => g.ToList());
   }

   public Dictionary<DateOnly, List<TotalCostsRecord>> GetCostsByDateFrom()
   {
      return CostsRecords.GroupBy(r => r.CostReport.DateFrom).ToDictionary(g => g.Key, g => g.ToList());
   }

   public decimal SummarisedCost()
   {
      return CostsRecords.Select(costs => costs.CostReport.Cost).Sum();
   }

}
