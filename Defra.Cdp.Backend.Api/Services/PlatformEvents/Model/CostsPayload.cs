using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.PlatformEvents.Model;

public class ServiceCodeCostsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("cost_reports")] public required List<ServiceCodeCostReportPayload> CostReports { get; init; }
}

public class TotalCostsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("cost_reports")] public required TotalCostReportPayload CostReports { get; init; }
}

public class ServiceCodeCostReportPayload
{
    [JsonPropertyName("serviceCode")] public required string ServiceCode { get; init; }
    [JsonPropertyName("serviceName")] public required string AwsService { get; init; }
    [JsonPropertyName("cost")] public required decimal Cost { get; init; }
    [JsonPropertyName("unit")] public required string Unit { get; init; }
    [JsonPropertyName("date_from")] public required DateOnly DateFrom { get; init; }
    [JsonPropertyName("date_to")] public required DateOnly DateTo { get; init; }
}

public class TotalCostReportPayload
{
    [JsonPropertyName("cost")] public required decimal Cost { get; init; }
    [JsonPropertyName("unit")] public required string Unit { get; init; }
    [JsonPropertyName("date_from")] public required DateOnly DateFrom { get; init; }
    [JsonPropertyName("date_to")] public required DateOnly DateTo { get; init; }
}