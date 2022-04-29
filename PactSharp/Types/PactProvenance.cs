using System.Text.Json.Serialization;
namespace PactSharp.Types;

public class PactProvenance
{
    public string TargetChainId { get; set; }
    public string ModuleHash { get; set; }

    [JsonConstructor]
    public PactProvenance(string targetChainId, string moduleHash)
    {
        TargetChainId = targetChainId;
        ModuleHash = moduleHash;
    }
}
