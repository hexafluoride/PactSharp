namespace PactSharp.Types;

public readonly struct PactClientSettings
{
    public readonly Network Network;
    public readonly string CustomNetworkEndpoint;
    public readonly string CustomNetworkId;

    public PactClientSettings(Network network, string customNetworkEndpoint, string customNetworkId)
    {
        Network = network;
        CustomNetworkEndpoint = customNetworkEndpoint;
        CustomNetworkId = customNetworkId;
    }
}

public enum Network
{
    Mainnet,
    Testnet,
    Local,
    Custom
}