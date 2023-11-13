using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using FluentAssertions;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github.ScheduledTasks;

public class QueryResponseTest
{
    [Fact]
    public void QueryResponseFromJson()
    {
        var queryJsonString = File.ReadAllText("Resources/example-repo-api-return.json");
        var response = JsonSerializer.Deserialize<QueryResponse>(queryJsonString);
        // Create a new ScheduledTasks instance
        var expectedResponse = new QueryResponse
        (
            new Data
            (
                new Organization
                (
                    "MDEyOk9yZ2FuaXphdGlvbjU1Mjg4MjI=",
                    new Teams
                    (
                        new PageInfo
                        (
                            false, "Y3Vyc29yOnYyOpMCsVdhdGVyIEFic3RyYWN0aW9uzgAlEAg="
                        ),
                        new List<TeamNodes>
                        {
                            new(
                                "address-facade-readers",
                                new Repositories(Enumerable.Empty<RepositoryNode>())),
                            new(
                                "analytics-standards",
                                new Repositories
                                (
                                    new List<RepositoryNode>
                                    {
                                        new(
                                            "analytics-standards",
                                            "Standards for service analytics",
                                            null!,
                                            "https://github.com/DEFRA/analytics-standards",
                                            false,
                                            false,
                                            false,
                                            DateTimeOffset.Parse("2020-08-21T10:38:11Z")
                                        )
                                    }
                                )
                            ),
                            new(
                                "cdp-platform",
                                new Repositories
                                (new List<RepositoryNode>
                                    {
                                        new(
                                            "cdp-node-frontend-template",
                                            "Core delivery platform Node.js Frontend Template. This is the template used to create new Node.js Frontend micro-services via the Core Development Portal.",
                                            new PrimaryLanguage("JavaScript"),
                                            "https://github.com/DEFRA/cdp-node-frontend-template",
                                            false,
                                            true,
                                            false,
                                            DateTimeOffset.Parse("2023-04-26T15:27:09Z")
                                        ),
                                        new(
                                            "cdp-boilerplate",
                                            "Testing repo creation from a GH template with a GH action",
                                            null!,
                                            "https://github.com/DEFRA/cdp-boilerplate",
                                            false,
                                            false,
                                            true,
                                            DateTimeOffset.Parse("2023-04-27T14:40:32Z")
                                        ),
                                        new(
                                            "cdp-user-service-backend",
                                            "Git repository for service cdp-user-service-backend",
                                            new PrimaryLanguage("JavaScript"),
                                            "https://github.com/DEFRA/cdp-user-service-backend",
                                            false,
                                            false,
                                            true,
                                            DateTimeOffset.Parse("2023-08-08T08:40:23Z")
                                        ),
                                        new(
                                            "cdp-dotnet-backend-template",
                                            "C# ASP.NET Minimial API template with MongoDB, FluentValidation, Swagger and Serilog logging",
                                            new PrimaryLanguage("C#"),
                                            "https://github.com/DEFRA/cdp-dotnet-backend-template",
                                            false,
                                            true,
                                            false,
                                            DateTimeOffset.Parse("2023-08-24T07:08:56Z")
                                        )
                                    }
                                )
                            )
                        }
                    )
                )
            )
        );
        response.Should().BeEquivalentTo(expectedResponse);
    }
}