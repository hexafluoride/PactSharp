using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactMetadata
{
    public long BlockTime { get; set; }
    public int BlockHeight { get; set; }
    
    public string BlockHash { get; set; }
    [JsonPropertyName("prevBlockHash")]
    public string PreviousBlockHash { get; set; }
    
    [JsonPropertyName("publicMeta")]
    public ChainwebMetadata PublicMetadata { get; set; }

    [JsonConstructor]
    public PactMetadata(long blockTime, int blockHeight, string blockHash, string previousBlockHash, ChainwebMetadata publicMetadata)
    {
        BlockTime = blockTime;
        BlockHeight = blockHeight;
        BlockHash = blockHash;
        PreviousBlockHash = previousBlockHash;
        PublicMetadata = publicMetadata;
    }
}
