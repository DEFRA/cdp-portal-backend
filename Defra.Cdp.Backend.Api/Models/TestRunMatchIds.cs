namespace Defra.Cdp.Backend.Api.Models;

// Used for linking an ECS event to a Test Run
public sealed record TestRunMatchIds(
    string TestSuite,
    string Environment,
    DateTime EventTime
);