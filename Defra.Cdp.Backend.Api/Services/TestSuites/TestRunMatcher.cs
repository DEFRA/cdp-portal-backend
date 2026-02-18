using Defra.Cdp.Backend.Api.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public record TestRunMatcher(
    string? Name = null,
    string? Environment = null,
    string[]? TestStatus = null,
    string[]? TaskStatus = null,
    DateTime? Start = null,
    DateTime? End = null
)
{
    public FilterDefinition<TestRun> Match()
    {
        var builder = Builders<TestRun>.Filter;
        var filter = builder.Empty;

        if (Name != null)
        {
            filter &= builder.Eq(t => t.TestSuite, Name);
        }

        if (Environment != null)
        {
            filter &= builder.Eq(t => t.Environment, Environment);
        }

        if (TestStatus is { Length: > 0 })
        {
            filter &= builder.In(t => t.TestStatus, TestStatus);
        }

        if (TaskStatus is { Length: > 0 })
        {
            filter &= builder.In(t => t.TaskStatus, TaskStatus);
        }

        if (Start != null && End != null)
        {
            filter &= builder.And(builder.Lte(t => t.Created, End), builder.Gte(t => t.TaskLastUpdate, Start));
        }

        return filter;
    }
}