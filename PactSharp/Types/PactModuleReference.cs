using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactModuleReference
{
    [JsonPropertyName("refSpec")]
    public List<PactRefSpec> Interfaces { get; set; } = new();
    
    [JsonPropertyName("refName")]
    public PactRefSpec Name { get; set; } = new("", "");

    public override string ToString()
    {
        return Name.ToString();
    }
}