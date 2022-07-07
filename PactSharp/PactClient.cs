using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PactSharp.Types;

namespace PactSharp;

public enum ServerType
{
    Chainweb,
    LocalPact
}

public class PactClient
{
    private readonly HttpClient _http;
    private PactClientSettings _settings;

    public string ApiHost { get; set; } = "";
    public string? P2PHost { get; set; }
    public string NetworkId { get; set; } = "";
    public ServerType ServerType { get; set; }

    public int DefaultGasLimit { get; set; } = 1500;
    public decimal DefaultGasPrice { get; set; } = 0.00000001m;
    public int DefaultTtl { get; set; } = 3600;
    public Network CurrentNetwork { get; set; }
    
    public static JsonSerializerOptions PactJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public List<string> RecognizedChains { get; set; } = new List<string>();

    public PactClient(HttpClient http)
    {
        this._http = http;
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json;blockheader-encoding=object,application/json");
    }
    
    public PactClient(HttpClient http, PactClientSettings settings) : this(http)
    {
        this._settings = settings;
    }

    public async Task UpdateSettings(PactClientSettings newSettings)
    {
        _settings = newSettings;
        await InitializeNetwork();
    }
    
    public async Task Initialize()
    {
        await InitializeNetwork();
    }

    private async Task InitializeNetwork()
    {
        var network = _settings.Network;
        CurrentNetwork = network;
        switch (network)
        {
            case Network.Mainnet:
                ApiHost = "https://api.chainweb.com";
                P2PHost = "https://us-e1.chainweb.com";
                NetworkId = "mainnet01";
                ServerType = ServerType.Chainweb;
                break;
            case Network.Testnet:
                ApiHost = "https://api.testnet.chainweb.com";
                NetworkId = "testnet04";
                ServerType = ServerType.Chainweb;
                break;
            case Network.Local:
                ApiHost = "http://localhost:8080";
                NetworkId = "";
                ServerType = ServerType.LocalPact;
                break;
            case Network.Custom:
                ApiHost = _settings.CustomNetworkRPCEndpoint;
                P2PHost = _settings.CustomNetworkP2PEndpoint;
                NetworkId = _settings.CustomNetworkId;
                ServerType = ServerType.Chainweb;
                break;
            default:
                throw new InvalidOperationException();
        }

        var shortTimeoutHttp = new HttpClient();
        shortTimeoutHttp.Timeout = TimeSpan.FromSeconds(2);

        try
        {
            var cutResp = await shortTimeoutHttp.GetStringAsync(GetApiUrl("/cut"));
            var cutRespParsed = JsonDocument.Parse(cutResp);
            RecognizedChains = cutRespParsed.RootElement.GetProperty("hashes").EnumerateObject()
                .Select(t => t.Name)
                .OrderBy(k => int.TryParse(k, out int n) ? n : -1)
                .ToList();

            if (!RecognizedChains.Any())
                throw new Exception();
        }
        catch
        {
            switch (ServerType)
            {
                case ServerType.Chainweb:
                    RecognizedChains = Enumerable.Range(0, 20).Select(i => i.ToString()).ToList();
                    break;
                case ServerType.LocalPact:
                    RecognizedChains = new List<string>() {"0"};
                    break;
            }
        }
    }

    private string GetP2PUrl(string endpoint, string? chain = null)
    {
        if (P2PHost == null)
            throw new Exception("Tried to call P2P API method without setting P2P host");

        switch (ServerType)
        {
            case ServerType.Chainweb:
                if (chain != null)
                    return $"{P2PHost}/chainweb/0.0/{NetworkId}/chain/{chain}{endpoint}";
                else
                    return $"{P2PHost}/chainweb/0.0/{NetworkId}{endpoint}";
            case ServerType.LocalPact:
                return $"{P2PHost}{endpoint}";
            default:
                throw new Exception($"Invalid ServerType {ServerType}");
        }
    }

    private string GetApiUrl(string endpoint, string? chain = null)
    {
        switch (ServerType)
        {
            case ServerType.Chainweb:
                if (chain != null)
                    return $"{ApiHost}/chainweb/0.0/{NetworkId}/chain/{chain}{endpoint}";
                else
                    return $"{ApiHost}/chainweb/0.0/{NetworkId}{endpoint}";
            case ServerType.LocalPact:
                return $"{ApiHost}{endpoint}";
            default:
                throw new Exception($"Invalid ServerType {ServerType}");
        }
    }

    public async Task<string?> SendTransactionAsync(PactCommand command)
    {
        var chain = command.Command?.Metadata.ChainId;

        var req = new {cmds = new[] {command}};
        var resp = await _http.PostAsJsonAsync(GetApiUrl($"/pact/api/v1/send", chain), req, PactJsonOptions);

        var respString = await resp.Content.ReadAsStringAsync();
        
        try
        {
            var parsedResponse = JsonDocument.Parse(respString);
            return parsedResponse.RootElement.GetProperty("requestKeys")[0].GetString();
        }
        catch (Exception e)
        {
            return e.Message + "\n" + respString;
        }
    }

    public async Task<PactCommandResponse> ExecuteLocalAsync(PactCommand command)
    {
        var chain = command.Command?.Metadata.ChainId;
        var resp = await _http.PostAsJsonAsync(GetApiUrl($"/pact/api/v1/local", chain), command, PactJsonOptions);

        var respString = await resp.Content.ReadAsStringAsync();
        
        try
        {
            var parsedResponse = await JsonSerializer.DeserializeAsync<PactCommandResponse>(await resp.Content.ReadAsStreamAsync(), PactJsonOptions);
            if (parsedResponse == null)
                throw new NullReferenceException($"Error deserializing PactCommandResponse from {resp}");

            parsedResponse.SourceCommand = command;
            return parsedResponse;
        }
        catch (Exception e)
        {
            return new PactCommandResponse(new PactCommandResult() {Status = "failure", Error = new PactError() {Message = respString, Info = e.Message, CallStack = e.StackTrace?.Split('\n')}})
            {
                SourceCommand = command
            };
        }
    }

    public async Task<PactCommandResponse?> PollRequestAsync(string chain, string requestKey)
    {
        var resp = await PollRequestsAsync(chain, new[] {requestKey});

        if (resp == null || !resp.ContainsKey(requestKey))
            return null;

        return resp[requestKey];
    }

    public async Task<Dictionary<string, int>> GetCutHeights()
    {
        var cutResp = await _http.GetStringAsync(GetApiUrl("/cut"));
        var cutRespParsed = JsonDocument.Parse(cutResp);
        var ret = new Dictionary<string, int>();

        foreach (var p in cutRespParsed.RootElement.GetProperty("hashes").EnumerateObject())
        {
            var chain = p.Name;
            var height = p.Value.GetProperty("height").GetInt32();
            ret[chain] = height;
        }

        return ret;
    }

    public async Task<IEnumerable<string>> MempoolGetPendingRequests(string chain)
    {
        var req = new {};
        var resp = await _http.PostAsJsonAsync(GetP2PUrl($"/mempool/getPending", chain), req, PactJsonOptions);

        var respElem = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync(), PactJsonOptions);

        return respElem.GetProperty("hashes").EnumerateArray().Select(e => e.GetString() ?? throw new Exception("Unexpected non-string in hashes array")).ToArray();
    }

    public async Task<Dictionary<string, PactCommand?>> MempoolLookupRequests(string chain, IEnumerable<string> keys)
    {
        var keysArr = keys.ToArray();
        var resp = await _http.PostAsJsonAsync(GetP2PUrl($"/mempool/lookup", chain), keysArr, PactJsonOptions);

        var respElem = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync(), PactJsonOptions);
        var respArr = respElem.EnumerateArray().ToArray();

        var ret = new Dictionary<string, PactCommand?>();

        for (var i = 0; i < respArr.Length; i++)
        {
            var key = keysArr[i];
            var txRes = respArr[i];
            var tag = txRes.GetProperty("tag").GetString() ?? throw new Exception("Unexpected non-string in tag field");
            if (tag == "Pending")
            {
                var rawCommand = txRes.GetProperty("contents").GetString() ?? throw new Exception("Unexpected non-string in contents field");
                var command = JsonSerializer.Deserialize<PactCommand>(rawCommand, PactJsonOptions);
                if (command != null && command.CommandEncoded != null && command.Hash != null) {
                    command.SetCommand(command.CommandEncoded);
                    ret[key] = command;
                } else {
                    throw new Exception("Could not deserialize inner PactCmd");
                }

                Debug.Assert(command?.Hash == key, "request key hash did not match expected value");
            } else {
                ret[key] = null;
            }
        }

        return ret;
    }
    
    public async Task<Dictionary<string, PactCommandResponse?>> PollRequestsAsync(string chain, string[] keys)
    {
        var req = new { requestKeys = keys };
        var resp = await _http.PostAsJsonAsync(GetApiUrl($"/pact/api/v1/poll", chain), req, PactJsonOptions);

        var respString = await resp.Content.ReadAsStringAsync();
        var respDict = new Dictionary<string, PactCommandResponse?>();
        
        var respObj = JsonNode.Parse(respString)?.AsObject();

        if (respObj?.Any() == true)
        {
            foreach (var (key, value) in respObj)
            {
                try
                {
                    respDict[key] = value.Deserialize<PactCommandResponse>(PactJsonOptions);
                }
                catch
                {
                    throw; // TODO: Error handling
                }
            }
        }

        foreach (var key in keys)
        {
            if (!respDict.ContainsKey(key))
                respDict[key] = null;
        }

        return respDict;
    }

    public async Task<ChainwebBlockHeader?> GetBlockHeaderAsync(string chain, string blockHash)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, GetApiUrl($"/header/{blockHash}?t=json", chain));
        req.Headers.Remove("Accept");
        req.Headers.TryAddWithoutValidation("Accept", "application/json;blockheader-encoding=object");
        var resp = await _http.SendAsync(req);

        try
        {
            return JsonSerializer.Deserialize<ChainwebBlockHeader>(await resp.Content.ReadAsStreamAsync(), PactJsonOptions);
        }
        catch
        {
            throw;
        }
    }
    
    public async Task<IEnumerable<ChainwebBlockHeader?>> GetBlockHeadersAsync(string chain, int minHeight, int maxHeight = -1)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, GetApiUrl($"/header?minheight={minHeight}{(maxHeight != -1 ? "&maxheight=" + maxHeight : "")}&t=json", chain));
        req.Headers.Remove("Accept");
        req.Headers.TryAddWithoutValidation("Accept", "application/json;blockheader-encoding=object");
        var resp = await _http.SendAsync(req);

        try
        {
            var parsed = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            return parsed.RootElement.GetProperty("items").EnumerateArray()
                .Select(e => e.Deserialize<ChainwebBlockHeader>(PactJsonOptions));
        }
        catch
        {
            throw;
        }
    }

    public async Task<ChainwebBlockPayload?> GetBlockPayloadAsync(string chain, string payloadHash)
    {
        var resp = await _http.GetAsync(GetApiUrl($"/payload/{payloadHash}/outputs", chain));

        try
        {
            return JsonSerializer.Deserialize<ChainwebBlockPayload>(await resp.Content.ReadAsStreamAsync(), PactJsonOptions);
        }
        catch
        {
            throw;
        }
    }

    public async Task<string> ObtainSpvAsync(string sourceChain, string targetChainId, string requestKey)
    {
        var req = new
        {
            targetChainId,
            requestKey
        };

        var resp = await _http.PostAsJsonAsync(GetApiUrl($"/pact/spv", sourceChain), req, PactJsonOptions);
        return (await resp.Content.ReadAsStringAsync()).Trim('"');
    }

    public PactCmd GenerateExecCommand(string chain, string code, object? data = null)
    {
        var cmd = new PactCmd(GenerateMetadata(chain), NetworkId)
        {
            Nonce = DateTime.UtcNow.ToLongDateString().HashEncoded(),
            Signers = new List<PactSigner>(),
            Payload = new PactPayload()
            {
                Exec = new PactExecPayload()
                {
                    Code = code,
                    Data = JsonObject.Create(JsonSerializer.SerializeToElement(data, PactJsonOptions)) ?? new JsonObject()
                }
            }
        };

        return cmd;
    }

    public ChainwebMetadata GenerateMetadata(string chain, int gasLimit = -1, decimal gasPrice = -1m,
        string sender = "", int ttl = -1,
        DateTime creationTime = default)
    {
        var _creationTime = (long) ((creationTime != default ? creationTime : DateTime.UtcNow) - DateTime.UnixEpoch).TotalSeconds;
        var _gasLimit = gasLimit < 0 ? DefaultGasLimit : gasLimit;
        var _gasPrice = gasPrice < 0 ? DefaultGasPrice : gasPrice;
        var _ttl = ttl < 0 ? DefaultTtl : ttl;
        return new ChainwebMetadata(chain, sender, _gasLimit, _gasPrice, _ttl, _creationTime);
    }

    public PactCommand BuildCommand(PactCmd cmd, PactSignature[]? signatures = null)
    {
        var ret = new PactCommand();
        
        ret.Command = cmd;
        ret.Signatures = signatures ?? Array.Empty<PactSignature>();
        ret.UpdateHash();

        return ret;
    }
}
