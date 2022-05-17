using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactPayload
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PactExecPayload? Exec { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PactContPayload? Cont { get; set; }
}
