using YamlDotNet.Serialization;

namespace PactSharp.Types;

class YamlPactObjectFactory : IObjectFactory
{
    private readonly IObjectFactory _fallback;

    public YamlPactObjectFactory(IObjectFactory fallback)
    {
        this._fallback = fallback;
    }

    public object Create(Type type)
    {
        if (type == typeof(PactCmd))
            return new PactCmd((ChainwebMetadata)Create(typeof(ChainwebMetadata)), "mainnet01");
        
        if (type == typeof(ChainwebMetadata))
            return new ChainwebMetadata("0", "", 1500, 1e-8m, 3600,
                (long) (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds);

        return _fallback.Create(type);
    }
}