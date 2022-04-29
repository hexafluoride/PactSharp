using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactContinuationMetadata
{
    [JsonPropertyName("args")]
    public object[] Arguments { get; set; }
    public string Def { get; set; }

    [JsonConstructor]
    public PactContinuationMetadata(object[] arguments, string def)
    {
        Arguments = arguments;
        Def = def;
    }
}
