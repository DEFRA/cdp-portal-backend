using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace Defra.Cdp.Backend.Api.Models;

public class Layer
{
    public Layer(string digest, List<LayerFile> files)
    {
        Digest = digest;
        Files = files;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; set; }

    public string Digest { get; set; }

    public List<LayerFile> Files { get; set; }
}

public record LayerFile(string FileName, string Content);