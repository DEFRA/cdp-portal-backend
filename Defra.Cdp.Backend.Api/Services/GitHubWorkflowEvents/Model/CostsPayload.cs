using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class ServiceCodeCostsPayload
{
   [JsonPropertyName("environment")] public required string Environment { get; init; }

   [JsonPropertyName("cost-reports")] public required List<CostReportPayload> costReports { get; init; }
}

public class EnvironmentCostsPayload
{
   [JsonPropertyName("environment")] public required string Environment { get; init; }

   [JsonPropertyName("cost-reports")] public required CostReportPayload costReports { get; init; }
}

public class CostReportPayload
{
   [JsonPropertyName("serviceCode")] public required string? serviceCode { get; init; }
   [JsonPropertyName("serviceName")] public required string? awsServiceName { get; init; }
   [JsonPropertyName("cost")] public required decimal cost { get; init; }
   [JsonPropertyName("unit")] public required string unit { get; init; }
   [JsonPropertyName("date_from")] public required DateOnly dateFrom { get; init; }
   [JsonPropertyName("date_to")] public required DateOnly dateTo { get; init; }
}
