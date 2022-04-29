using System.Text.Json.Serialization;
namespace PactSharp.Types;

public class PactContPayload
{
    public string PactId { get; set; }
    public bool Rollback { get; set; }
    public int Step { get; set; }
    public string Proof { get; set; }
    public object? Data { get; set; }

    [JsonConstructor]
    public PactContPayload(string pactId, bool rollback, int step, string proof, object data)
    {
        PactId = pactId;
        Rollback = rollback;
        Step = step;
        Proof = proof;
        Data = data;
    }
}
