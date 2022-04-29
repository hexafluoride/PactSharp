using System.Text.Json.Serialization;

namespace PactSharp.Types;

public class PactEvent
{
    public string Name { get; set; }
    
    [JsonPropertyName("params")]
    public object[] Parameters { get; set; }
    
    public PactModuleReference Module { get; set; }
    public string ModuleHash { get; set; }

    [JsonConstructor]
    public PactEvent(string name, object[] parameters, PactModuleReference module, string moduleHash)
    {
        Name = name;
        Parameters = parameters;
        Module = module;
        ModuleHash = moduleHash;
    }
}
