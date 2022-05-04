using System.Text.Json.Serialization;
using PactSharp.Services;

namespace PactSharp.Types;

public class PactCommandResponse : ICacheable
{
    public string CacheKey => RequestKey == null ? "" : GetCacheKey(RequestKey);

    public static string GetCacheKey(string requestKey) => $"command-response@{requestKey}";
    
    [JsonIgnore]
    public PactCommand? SourceCommand { get; set; }
    
    [JsonPropertyName("reqKey")]
    public string? RequestKey { get; set; }
    public PactCommandResult Result { get; set; }
    
    [JsonPropertyName("txId")]
    [JsonConverter(typeof(StringCoercingJsonConverter))]
    public string? TransactionId { get; set; }
    public long? Gas { get; set; }
    public string? Logs { get; set; }
    public PactMetadata? Metadata { get; set; }
    public PactContinuation? Continuation { get; set; }
    public PactEvent[]? Events { get; set; }

    public PactCommandResponse(PactCommandResult result)
    {
        Result = result;
    }
}
