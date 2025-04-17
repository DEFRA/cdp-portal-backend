namespace Defra.Cdp.Backend.Api.Services.Migrations;

public static class CodeBuildStatuses
{
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Fault = "FAULT";
    public const string InProgress = "IN_PROGRESS";
    public const string Stopped = "STOPPED";
    public const string TimedOut = "TIMED_OUT";
    public const string Requested = "REQUESTED";
}