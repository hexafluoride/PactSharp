using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactSignature
{
    [JsonPropertyName("sig")]
    public string? Signature { get; set; }

    public PactSignature(string signature)
    {
        Signature = signature;
    }

    public PactSignature()
    {
    }
}
