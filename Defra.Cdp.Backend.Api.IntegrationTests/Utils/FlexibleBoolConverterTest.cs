using Defra.Cdp.Backend.Api.Utils.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Utils;

public class FlexibleBoolConverterTest
{
    private class TestModel
    {
        [JsonConverter(typeof(FlexibleBoolConverter))]
        public bool Value { get; set; }
    }

    [Theory]
    [InlineData("{\"Value\": true}", true)]
    [InlineData("{\"Value\": false}", false)]
    [InlineData("{\"Value\": \"true\"}", true)]
    [InlineData("{\"Value\": \"false\"}", false)]
    public void FlexibleBoolConverter_ShouldDeserializeVariousFormats(string json, bool expected)
    {
        var obj = JsonSerializer.Deserialize<TestModel>(json);
        Assert.Equal(expected, obj!.Value);
    }

    [Fact]
    public void FlexibleBoolConverter_ShouldThrowOnInvalidValue()
    {
        var json = "{\"Value\": \"notabool\"}";
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<TestModel>(json);
        });
    }
    
}