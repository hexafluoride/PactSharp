using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactEvent
{
    public string Name { get; set; }
    private List<object> _params = new();

    [JsonPropertyName("params")]
    public List<object> Parameters
    {
        get => _params;
        set
        {
            _params = value;
            PromoteArgumentsFromJsonElement();
        }
    }

    private void PromoteArgumentsFromJsonElement()
    {
        object PromoteObject(JsonElement elem)
        {
            var elemObj = JsonObject.Create(elem);

            if (elemObj == null)
                return elem;
            
            if (elemObj.ContainsKey("refName"))
                return elemObj.Deserialize(typeof(PactModuleReference), PactClient.PactJsonOptions) ?? elem;

            if (elemObj.ContainsKey("decimal"))
                return decimal.Parse(elemObj["decimal"]?.GetValue<string>() ?? "0");

            if (elemObj.ContainsKey("int"))
                return BigInteger.Parse(elemObj["int"]?.GetValue<string>() ?? "0");
            
            return elemObj;
        }
        
        Func<JsonElement, object> resolve = (e) => e;
        resolve = (elem) =>
        {
            return elem.ValueKind switch
            {
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Number => elem.GetDecimal(),
                JsonValueKind.String => elem.GetString() ?? "",
                JsonValueKind.Array => elem.EnumerateArray().Select(e => resolve(e)).ToArray(),
                JsonValueKind.Object => PromoteObject(elem),
                _ => elem
            };
        };
        
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (Parameters[i] is JsonElement jsonElement)
                Parameters[i] = resolve(jsonElement);
        }
    }
    
    public PactRefSpec Module { get; set; }
    public string ModuleHash { get; set; }

    [JsonConstructor]
    public PactEvent(string name, List<object> parameters, PactRefSpec module, string moduleHash)
    {
        Name = name;
        Parameters = parameters;
        Module = module;
        ModuleHash = moduleHash;
    }
}
