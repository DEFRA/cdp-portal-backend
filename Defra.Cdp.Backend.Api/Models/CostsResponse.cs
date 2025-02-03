using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ServiceCodesCostsResponse
{
   public ServiceCodesCostsResponse(ServiceCodesCosts costsRecords)
   {
      ServiceCodes = new Dictionary<string, EnvironmentsCostsByServiceCodeResponse>();
      foreach (var serviceCode in costsRecords.ListServiceCodes())
      {
         var costs = costsRecords.GetCosts(serviceCode);
         var environmentsCosts = new EnvironmentsCostsByServiceCodeResponse(serviceCode, costs);
         ServiceCodes.Add(serviceCode, environmentsCosts);
      }
   }

   [JsonPropertyName("serviceCodes")] public Dictionary<string, EnvironmentsCostsByServiceCodeResponse> ServiceCodes { get; }
}

public class EnvironmentsCostsByServiceCodeResponse
{
   public EnvironmentsCostsByServiceCodeResponse(string serviceCode, EnvironmentsCostsByServiceCode costsRecords)
   {
      EnvironmentsCostRecords = new Dictionary<string, List<CostsResponse>>();
      foreach (var environment in costsRecords.ListEnvironments())
      {
         var costs = costsRecords.GetCosts(environment);
         var costsResponses = costs.GetCosts().Select(costs => new CostsResponse(costs)).ToList();
         EnvironmentsCostRecords.Add(environment, costsResponses);
      }
   }

   [JsonPropertyName("serviceCode")] public string ServiceCode { get; }

   [JsonPropertyName("environments")] public Dictionary<string, List<CostsResponse>> EnvironmentsCostRecords { get; }

}

public class CostsResponse
{
   public CostsResponse(ServiceCodeCostsRecord CostsRecord)
   {
      Cost = CostsRecord.CostReport.Cost;
      Currency = CostsRecord.CostReport.Currency;
   }

   [JsonPropertyName("cost")] public decimal Cost { get; }

   [JsonPropertyName("currency")] public string Currency { get; }
}
