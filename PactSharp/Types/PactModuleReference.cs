using System.Text.Json.Serialization;
namespace PactSharp.Types;

public class PactModuleReference
{
    public string Namespace { get; set; }
    public string Name { get; set; }

    [JsonConstructor]
    public PactModuleReference(string @namespace, string name)
    {
        Namespace = @namespace;
        Name = name;
    }
}
