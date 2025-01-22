using Defra.Cdp.Backend.Api.Services.Github;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Defra.Cdp.Backend.Api.Tests.Services.Github;

public class TemplatesLookupTest
{
    [Fact]
    public void TemplatesReadFromConfigurationCorrectly()
    {
        var myConfiguration = new Dictionary<string, string>
        {
            { "TemplateMappings:cdp-node-frontend-template:Description", "Node.js Frontend" },
            { "TemplateMappings:cdp-node-frontend-template:Language", "node" },
            { "TemplateMappings:cdp-node-frontend-template:Type", "frontend" },
            { "TemplateMappings:cdp-node-frontend-template:Zone", "public" },
            { "TemplateMappings:cdp-node-backend-template:Description", "Node.js Backend" },
            { "TemplateMappings:cdp-node-backend-template:Language", "node" },
            { "TemplateMappings:cdp-node-backend-template:Type", "backend" },
            { "TemplateMappings:cdp-node-backend-template:Zone", "protected" },
            { "TemplateMappings:cdp-dotnet-backend-template:Description", "DotNet Backend" },
            { "TemplateMappings:cdp-dotnet-backend-template:Language", "dotnet" },
            { "TemplateMappings:cdp-dotnet-backend-template:Type", "backend" },
            { "TemplateMappings:cdp-dotnet-backend-template:Zone", "protected" },
            { "TemplateMappings:cdp-python-backend-template:Description", "Python Backend" },
            { "TemplateMappings:cdp-python-backend-template:RequiredScope", "pythonUser" },
            { "TemplateMappings:cdp-python-backend-template:Language", "python" },
            { "TemplateMappings:cdp-python-backend-template:Type", "backend" },
            { "TemplateMappings:cdp-python-backend-template:Zone", "protected" },
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration!)
            .Build();

        var templatesLookup = new TemplatesFromConfig(configuration);
        var expected = new List<ServiceTemplate>
        {
            new("cdp-dotnet-backend-template", "DotNet Backend", "dotnet", null, "backend",  "protected"),
            new("cdp-node-backend-template", "Node.js Backend", "node", null, "backend",  "protected"),
            new("cdp-node-frontend-template", "Node.js Frontend", "node", null, "frontend",  "public"),
            new("cdp-python-backend-template", "Python Backend", "python", "pythonUser", "backend",  "protected")
        };

        templatesLookup._templates.Should().Equal(expected);
    }
}