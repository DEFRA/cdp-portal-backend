using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.AspNetCore.Mvc;
using Defra.Cdp.Backend.Api.Models;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class CostsEndpoint
{
   private static ILogger? _logger;

   public static void MapCostsEndpoint(this IEndpointRouteBuilder app)
   {
      // app.MapGet("/costs", FindCombinedCosts);
      // app.MapGet("/costs/total", FindTotalCosts);
      app.MapGet("/costs/servicecode", FindServiceCodeCosts);
   }

   static async Task<IResult> FindServiceCodeCosts(
       [FromQuery(Name = "from")] DateOnly dateFrom,
       [FromQuery(Name = "to")] DateOnly dateTo,
       [FromServices] IServiceCodeCostsService serviceCodeCostsService,
       CancellationToken cancellationToken,
       ILoggerFactory loggerFactory,
       [FromQuery(Name = "timeunit")] string timeUnit = "day")
   {
      var reportTimeUnit = timeUnit switch
      {
         "month" => ReportTimeUnit.Monthly,
         "30day" => ReportTimeUnit.ThirtyDays,
         "day" => ReportTimeUnit.Daily,
         _ => throw new ArgumentOutOfRangeException(nameof(timeUnit), timeUnit, null)
      };
      var result = await serviceCodeCostsService.FindAllCosts(reportTimeUnit, dateFrom, dateTo, cancellationToken);

      return result == null
        ? Results.NotFound(new ApiError("Not found"))
        : Results.Ok(new ServiceCodesCostsResponse(result));
   }
}
