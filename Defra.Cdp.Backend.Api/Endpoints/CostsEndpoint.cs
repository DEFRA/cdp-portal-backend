using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class CostsEndpoint
{
   public static void MapCostsEndpoint(this IEndpointRouteBuilder app)
   {
      // app.MapGet("/costs/total/daily/environment", FindAllEnvironmentsTotalCosts);
      // app.MapGet("/costs/total/monthly/environment", FindAllEnvironmentsTotalCosts);
      // app.MapGet("/costs/total/daily/environment/{environment}/daily", FindEnvironmentTotalCost);
      // app.MapGet("/costs/total/monthly/environment/{environment}/monthly", FindEnvironmentTotalCost);
      // app.MapGet("/costs/daily/servicecode/{serviceCode}/environment/{environment}/daily", FindServiceCodeInEnvironmentCosts);
      // app.MapGet("/costs/monthly/servicecode/{serviceCode}/environment/{environment}/monthly", FindServiceCodeInEnvironmentCosts);
      // app.MapGet("/costs/daily/servicecode/environment/{environment}/daily", FindServiceCodeInEnvironmentCosts);
      // app.MapGet("/costs/monthly/servicecode/environment/{environment}/monthly", FindServiceCodeInEnvironmentCosts);
      // app.MapGet("/costs/daily/servicecode/{serviceCode}/daily", FindServiceCodeInAllEnvironmentsCosts);
      // app.MapGet("/costs/monthly/servicecode/{service/Code}", FindServiceCodeCosts);
      // app.MapGet("/costs/daily/servicecode/daily", FindAllServiceCodesInAllEnvironmentsCosts);
      // app.MapGet("/costs/monthly/servicecode/monthly", FindAllServiceCodesInAllEnvironmentsCosts);
      // app.MapGet("/costs/servicecode/{serviceCode}", FindServiceCodeCosts);
      app.MapGet("/costs", FindAllServiceCodeCosts);
   }

   private static async Task<IResult> FindAllServiceCodeCosts(
       IServiceCodeCostsService costService,
       [FromQuery(Name = "service_codes")] string[]? serviceCodes,
       [FromQuery(Name = "environments")] string[]? environments,
       [FromQuery(Name = "from")] string? dateFrom,
       [FromQuery(Name = "to")] string? dateTo,
       [FromQuery(Name = "unit")] string? dateUnit,// = CostUnit.month.toString(),
       ILoggerFactory loggerFactory,
       CancellationToken cancellationToken)
   {
      var logger = loggerFactory.CreateLogger("CostsEndpoint");
      logger.LogInformation("Costs for service codes {serviceCodes} in environments {environments} from {dateFrom} to {dateTo} in unit {unit}", serviceCodes, environments, dateFrom, dateTo, dateUnit);

      var result = await costService.FindCosts(environments, dateFrom, dateTo, cancellationToken);
      return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(new ServiceCodeCostsResponse(result));

   }


   public enum CostUnit
   {
      day,
      month
   }


   private class ServiceCodeCostsResponse
   {
      public ServiceCodeCostsResponse(List<ServiceCodeCostsRecord> serviceCodeCostsRecord)
      {
      }
   }

   // private class EnvironmentCostsResponse
   // {
   //    public EnvironmentCostsResponse(EnvironmentCostsRecord EnvironmentCostsRecord)
   //    {
   //    }
   // }

}
