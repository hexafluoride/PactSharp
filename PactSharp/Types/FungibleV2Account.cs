using System.Text.Json.Serialization;
using PactSharp.Services;

namespace PactSharp.Types;

public class FungibleV2Account : ICacheable
{
    public string CacheKey => GetCacheKey(Network, Chain, Module, Account);
    public static string GetCacheKey(string network, string chain, string module, string? account) =>
        $"fungible-v2@{network}${chain}${module}${account}";
    
    public string Network { get; set; }
    public string Chain { get; set; }
    public string Module { get; set; }
    public string? Account { get; set; }
    public decimal? Balance { get; set; }
    public object? Guard { get; set; }

    [JsonConstructor]
    public FungibleV2Account(string network, string chain, string module)
    {
        Network = network;
        Chain = chain;
        Module = module;
    }

    public static FungibleV2Account FromResponse(PactCommandResponse response, string module)
    {
        var networkId = response.SourceCommand?.Command?.NetworkId;
        var chainId = response.SourceCommand?.Command?.Metadata.ChainId;
        if (networkId == null || chainId == null)
            throw new Exception("Cannot build FungibleV2Account from response without networkId/chainId fields");

        var ret = new FungibleV2Account(networkId, chainId,  module);

        if (response.Result?.Status != "success")
            return ret;

        ret.Guard = response.Result.Data?.GetProperty("guard");
        ret.Balance = response.Result.Data?.GetProperty("balance").GetDecimal();
        ret.Account = response.Result.Data?.GetProperty("account").GetString();

        return ret;
    }
}
