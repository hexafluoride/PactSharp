using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactRefSpec
{
    public string Namespace { get; set; }
    [JsonPropertyName("name")]
    public string Module { get; set; }

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Namespace))
            return Module;
        return $"{Namespace}.{Module}";
    }
    
    [JsonConstructor]
    public PactRefSpec(string @namespace, string module)
    {
        Namespace = @namespace;
        Module = module;
    }
}
