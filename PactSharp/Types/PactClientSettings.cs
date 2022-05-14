namespace PactSharp.Types;

public readonly struct PactClientSettings
{
    public readonly Network Network;
    public readonly string CustomNetworkRPCEndpoint;
    public readonly string? CustomNetworkP2PEndpoint;
    public readonly string CustomNetworkId;

    public PactClientSettings(Network network, string customNetworkRpcEndpoint, string customNetworkP2PEndpoint, string customNetworkId)
    {
        Network = network;
        CustomNetworkRPCEndpoint = customNetworkRpcEndpoint;
        CustomNetworkP2PEndpoint = customNetworkP2PEndpoint;
        CustomNetworkId = customNetworkId;
    }

    public PactClientSettings(Network network, string customNetworkRpcEndpoint, string customNetworkId)
    {
        Network = network;
        CustomNetworkRPCEndpoint = customNetworkRpcEndpoint;
        CustomNetworkP2PEndpoint = null;
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
