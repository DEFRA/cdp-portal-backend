using System.Collections.Immutable;
using Defra.Cdp.Backend.Api.Services.Github;
using Microsoft.Extensions.Configuration;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github;

public class TemplatesLookupTest
{
    [Fact]
    public void TemplatesReadFromConfigurationCorrectly()
    {
        var myConfiguration = new Dictionary<string, string>
        {
            { "TemplateMappings:cdp-node-frontend-template", "Node.js Frontend" },
            { "TemplateMappings:cdp-node-backend-template", "Node.js Backend" },
            { "TemplateMappings:cdp-dotnet-backend-template", "DotNet Backend" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var templatesLookup = new TemplatesFromConfig(configuration);
        var expected = ImmutableDictionary.CreateRange(
            new List<KeyValuePair<string, string>>
            {
                new("cdp-node-frontend-template", "Node.js Frontend"),
                new("cdp-node-backend-template", "Node.js Backend"),
                new("cdp-dotnet-backend-template", "DotNet Backend")
            }
        );

        Assert.Equal(expected, templatesLookup.Templates);
    }
}