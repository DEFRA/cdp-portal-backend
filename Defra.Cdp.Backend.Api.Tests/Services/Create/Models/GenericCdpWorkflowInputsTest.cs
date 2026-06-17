using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Create.Models;

public class GenericCdpWorkflowInputsTest
{
    [Fact]
    public void Inputs_writes_commands_as_json_string()
    {
        var commands = new List<string>
        {
            "tenant s3 add --service foo --environment all --name bucket"
        }
        ;
        var inputs = new GenericCdpWorkflowInputs(commands, "1234", "foo", "my pr");

        var result = JsonSerializer.Serialize(inputs);
        var expected = """{"run_id":"1234","commands":"[\u0022tenant s3 add --service foo --environment all --name bucket\u0022]","use_branch":"foo","pr_title":"my pr"}""";
        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }
}