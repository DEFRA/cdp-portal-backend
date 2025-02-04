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

   public Dictionary<String, List<ServiceCodeCostsRecord>> GetCostsByServiceCodes()
   {
      return CostsRecords.GroupBy(r => r.ServiceCode).ToDictionary(g => g.Key, g => g.ToList());
   }
   public Dictionary<String, List<ServiceCodeCostsRecord>> GetCostsByEnvironments()
   {
      return CostsRecords.GroupBy(r => r.Environment).ToDictionary(g => g.Key, g => g.ToList());
   }

}
