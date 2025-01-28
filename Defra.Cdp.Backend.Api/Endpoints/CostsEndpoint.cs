using Defra.Cdp.Backend.Api.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class CostsEndpoint
{
   public static void MapSquidProxyConfigEndpoint(this IEndpointRouteBuilder app)
   {
      app.MapGet("/cost/{serviceCode}/{environment}", FindServiceCodeCost);
      app.MapGet("/cost/{environment}", FindEnvironmentCost);
   }

   private static async Task<IResult> FindServiceCodeCost(
       IServiceCodeCostsService costService,
       string serviceCode,
       string environment,
       [FromQuery(Name = "year")] int year,
       [FromQuery(Name = "month")] string? month,
       [FromQuery(Name = "day")] int? day,
       ILoggerFactory loggerFactory,
       CancellationToken cancellationToken)
   {
      var logger = loggerFactory.CreateLogger("CostsEndpoint");
      logger.LogInformation("Cost for service code {serviceCode} in environment {environment} for {year}, {month}, {day}", serviceCode, environment, year, month, day);

      var result = await costService.FindCosts(environment, cancellationToken);
      return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(new ServiceCodeCostsResponse(result));
   }

   private static async Task<IResult> FindEnvironmentCost(
       IEnvironmentCostsService costService,
       string environment,
       [FromQuery(Name = "year")] int year,
       [FromQuery(Name = "month")] string? month,
       [FromQuery(Name = "day")] int? day,
       ILoggerFactory loggerFactory,
       CancellationToken cancellationToken)
   {
      var logger = loggerFactory.CreateLogger("CostsEndpoint");
      logger.LogInformation("Cost for environment {environment} for {year}, {month}, {day}", environment, year, month, day);

      var result = await costService.FindCost(environment, cancellationToken);
      return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(new EnvironmentCostsResponse(result));
   }

   private class ServiceCodeCostsResponse
   {
      public ServiceCodeCostsResponse(ServiceCodeCostsRecord serviceCodeCostsRecord)
      {
      }
   }
   private class EnvironmentCostsResponse
   {
      public EnvironmentCostsResponse(EnvironmentCostsRecord EnvironmentCostsRecord)
      {
      }
   }

}
