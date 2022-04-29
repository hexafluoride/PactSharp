using System.Text.Json;
using PactSharp.Types;

namespace PactSharp.Services;

public class ChainwebQueryService : IChainwebQueryService
{
    private PactClient PactClient { get; set; }
    private ICacheService _cache;

    private string Network => PactClient.NetworkId;

    public ChainwebQueryService(PactClient client, ICacheService cache)
    {
        PactClient = client;
        _cache = cache;
    }

    private async Task SaveCache()
    {
        await _cache.Flush();
    }

    public async Task<IEnumerable<FungibleV2Account?>> GetAccountDetailsAsync(string chain, string[] modules,
        AccountIdentifier[] accounts, bool ignoreCache = false)
    {
        if (!accounts.Any())
            return Array.Empty<FungibleV2Account>();
        
        var moduleMetadata = await GetModuleMetadataAsync(chain, modules);
        var modulesValidForChain = modules.Select(module =>
                new {Module = module, Metadata = moduleMetadata.First(m => m?.Name == module)})
            .Where(composite => (composite.Metadata?.Exists ?? false) && (composite.Metadata?.Interfaces?.Contains("fungible-v2") ?? false))
            .Select(composite => composite.Module)
            .ToList();

        var cacheKeys = modulesValidForChain.SelectMany(module =>
            accounts.Select(account => FungibleV2Account.GetCacheKey(Network, chain, module, account.Name))).ToList();

        if (!ignoreCache && cacheKeys.All(_cache.HasItem))
            return await Task.WhenAll(cacheKeys.Select(key => _cache.GetItem<FungibleV2Account>(key)));
        
        var code = @"
(namespace 'free)
(module " + new string(DateTime.UtcNow.ToString().HashEncoded().Where(char.IsLetter).ToArray()) + @" T
  (defcap T () true)
  (defun try-get-details (account:string token:module{fungible-v2}) { 'module: (format ""{}"" [token]), 'account: account, 'result: (try {} (token::details account))})
  (defun fetch-accounts (tokens:[module{fungible-v2}] account:string)
    (map (try-get-details account) tokens)
  )
  (defun fetch-accounts-many (tokens:[module{fungible-v2}] accounts:[string])
    (map (fetch-accounts tokens) accounts)
  )
)
(fetch-accounts-many [" + string.Join(' ', modulesValidForChain) + @"] (read-msg 'accounts))
";

        var detailsCmd = PactClient.GenerateExecCommand(chain, code, new {accounts = accounts.Select(a => a.Name)});
        detailsCmd.Metadata.GasLimit = 150000;
        var detailsCommand = PactClient.BuildCommand(detailsCmd);

        var resultAccounts = new List<FungibleV2Account>();
        
        var result = await PactClient.ExecuteLocalAsync(detailsCommand);

        if (result.Result.Status != "success")
        {
            return resultAccounts;
        }

        foreach (var subArray in result.Result.Data?.EnumerateArray() ?? Enumerable.Empty<JsonElement>())
        {
            foreach (var accountDetails in subArray.EnumerateArray())
            {
                var module = accountDetails.GetProperty("module").GetString();
                if (module == null)
                    throw new Exception($"Unexpected result from blockchain when fetching account details: {result.Result.Data}");

                var account = accountDetails.GetProperty("account").GetString();
                var accountObject = accountDetails.GetProperty("result");

                var ret = new FungibleV2Account(detailsCmd.NetworkId, chain, module)
                {
                    Account = account
                };

                if (accountObject.TryGetProperty("balance", out JsonElement _))
                {
                    ret.Guard = accountObject.GetProperty("guard");
                    ret.Balance = accountObject.GetProperty("balance").GetDecimal();
                    ret.Account = accountObject.GetProperty("account").GetString();
                }

                resultAccounts.Add(ret);
            }
        }

        await Task.WhenAll(resultAccounts.Select(account => _cache.SetItem(account, 30)));
        await _cache.Flush();

        return resultAccounts;
    }

    public async Task<FungibleV2Account?> GetAccountDetailsAsync(string chain, string module, AccountIdentifier account, bool ignoreCache = false)
    {
        return (await GetAccountDetailsAsync(new[] {module}, new[] {account}, ignoreCache))?.SingleOrDefault();
    }

    public async Task<IEnumerable<FungibleV2Account?>> GetAccountDetailsAsync(string module, AccountIdentifier account, bool ignoreCache = false)
    {
        return await ForAllChains(chain => GetAccountDetailsAsync(chain, module, account, ignoreCache));
    }

    public async Task<IEnumerable<FungibleV2Account?>> GetAccountDetailsAsync(string[] modules, AccountIdentifier[] accounts, bool ignoreCache = false)
    {
        return (await ForAllChains(chain => GetAccountDetailsAsync(chain, modules, accounts, ignoreCache)))
            .SelectMany(t => t);
    }
    
    public async Task<PactCommand?> FetchTransactionAsync(PactCommandResponse response)
    {
        var chainId = response.Metadata?.PublicMetadata.ChainId;
        var blockHash = response.Metadata?.BlockHash;
        if (chainId == null || blockHash == null || response.RequestKey == null)
            return null;
        else
            return await FetchTransactionAsync(chainId, blockHash,  response.RequestKey);
    }

    public async Task<PactCommand?> FetchTransactionAsync(string chain, string requestKey)
    {
        var resp = await PactClient.PollRequestAsync(chain, requestKey);
        var blockHash = resp?.Metadata?.BlockHash;
        if (resp == null || blockHash == null)
            return null;
        else
            return await FetchTransactionAsync(chain, blockHash, requestKey);
    }

    public async Task<PactCommand?> FetchTransactionAsync(string chain, string blockHash, string requestKey)
    {
        if (string.IsNullOrWhiteSpace(chain) || string.IsNullOrWhiteSpace(blockHash) ||
            string.IsNullOrWhiteSpace(requestKey))
            return null;
        
        ChainwebBlockPayload? blockPayload = await FetchBlockPayloadAsync(chain, blockHash);
        if (blockPayload == null)
            return null;

        return await blockPayload.GetTransaction(requestKey);
    }

    private async Task<T?> FetchAndCache<T>(string cacheKey, Func<Task<T?>> fetchAction) where T : ICacheable
    {
        T? result;

        if (_cache.HasItem(cacheKey))
            result = await _cache.GetItem<T>(cacheKey) ?? await fetchAction();
        else
            result = await fetchAction();

        if (string.Equals(result?.CacheKey, cacheKey) && result is T resultVal)
            await _cache.SetItem(resultVal);

        return result;
    }

    private async Task<ChainwebBlockPayload?> FetchBlockPayloadAsync(string chain, string blockHash)
    {
        var headerKey = ChainwebBlockHeader.GetCacheKey(blockHash);
        ChainwebBlockHeader? blockHeader =
            await FetchAndCache(headerKey, () => PactClient.GetBlockHeaderAsync(chain, blockHash));
        
        if (blockHeader == null)
            return null;

        var payloadHash = blockHeader.PayloadHash;
        var payloadKey = ChainwebBlockPayload.GetCacheKey(payloadHash);
        ChainwebBlockPayload? blockPayload =
            await FetchAndCache(payloadKey, () => PactClient.GetBlockPayloadAsync(chain, payloadHash));

        return blockPayload;
    }

    public async Task<bool> ModuleExistsAsync(string chain, string module)
    {
        var moduleMetadata = await GetModuleMetadataAsync(chain, module);
        return moduleMetadata?.Exists ?? false;
    }

    public async Task<PactModuleMetadata?> GetModuleMetadataAsync(string chain, string module)
    {
        return (await GetModuleMetadataAsync(chain, new[] {module})).Single();
    }

    public async Task<IEnumerable<PactModuleMetadata?>> GetModuleMetadataAsync(string chain, string[] modules)
    {
        var cacheKeys = modules.Select(module => new
        {
            Module = module, Key = PactModuleMetadata.GetCacheKey(Network, chain, module)
        }).Select(composite => new { composite.Module, composite.Key, Cached = _cache.HasItem(composite.Key) }).ToList();
        
        var uncached = cacheKeys.Where(k => !k.Cached);
        var cached = cacheKeys.Where(k => k.Cached);
        var results = (await Task.WhenAll(cached.Select(k => _cache.GetItem<PactModuleMetadata>(k.Key)))).ToList(); 

        if (!uncached.Any())
        {
            return results;
        }

        var queryModules = uncached.Select(k => k.Module);
        var queryStatements =
            queryModules.Select(module => $"{{ 'module: \"{module}\", 'metadata: (try {{}} (describe-module \"{module}\")) }}");
        var query = $"[{string.Join(' ', queryStatements)}]";

        var queryCmd = PactClient.GenerateExecCommand(chain, query);
        var queryCommand = PactClient.BuildCommand(queryCmd);

        var result = await PactClient.ExecuteLocalAsync(queryCommand);

        if (result.Result.Status != "success")
        {
            throw new PactExecutionException(result);
        }

        foreach (var queryResult in result.Result.Data?.EnumerateArray() ?? Enumerable.Empty<JsonElement>())
        {
            var moduleName = queryResult.GetProperty("module").GetString();
            if (moduleName == null)
                throw new Exception($"Unexpected result from blockchain when fetching module metadata: {result.Result.Data}");

            var moduleMetadata = queryResult.GetProperty("metadata");

            var metadataObject = new PactModuleMetadata(PactClient.NetworkId, chain, moduleName);

            if (moduleMetadata.TryGetProperty("hash", out JsonElement _))
            {
                metadataObject.Hash = moduleMetadata.GetProperty("hash").GetString();
                metadataObject.Blessed = moduleMetadata.GetProperty("blessed").EnumerateArray().Select(t => t.GetString()).OfType<string>().ToArray();
                metadataObject.Code = moduleMetadata.GetProperty("code").GetString();
                metadataObject.Governance = moduleMetadata.GetProperty("keyset").GetString();
                metadataObject.Interfaces = moduleMetadata.GetProperty("interfaces").EnumerateArray().Select(t => t.GetString()).OfType<string>().ToArray();
            }

            await _cache.SetItem(metadataObject);
            results.Add(metadataObject);
        }

        await SaveCache();
        return results;
    }

    private async Task<IEnumerable<TResult>> ForAllChains<TResult>(Func<string, Task<TResult>> app) =>
        await Task.WhenAll(PactClient.RecognizedChains.Select(app));
}

public interface IChainwebQueryService
{
    Task<FungibleV2Account?> GetAccountDetailsAsync(string chain, string module, AccountIdentifier account, bool ignoreCache = false);
    Task<IEnumerable<FungibleV2Account?>> GetAccountDetailsAsync(string module, AccountIdentifier account, bool ignoreCache = false);
    Task<IEnumerable<FungibleV2Account?>> GetAccountDetailsAsync(string chain, string[] modules, AccountIdentifier[] accounts, bool ignoreCache = false);
    Task<IEnumerable<FungibleV2Account?>> GetAccountDetailsAsync(string[] modules, AccountIdentifier[] accounts, bool ignoreCache = false);
    Task<PactCommand?> FetchTransactionAsync(PactCommandResponse response);
    Task<PactCommand?> FetchTransactionAsync(string chain, string requestKey);
    Task<PactCommand?> FetchTransactionAsync(string chain, string blockHash, string requestKey);
    Task<IEnumerable<PactModuleMetadata?>> GetModuleMetadataAsync(string chain, string[] modules);
    Task<PactModuleMetadata?> GetModuleMetadataAsync(string chain, string module);
    Task<bool> ModuleExistsAsync(string chain, string module);
}
