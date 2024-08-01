namespace Libplanet.Node.Options;

public sealed class NodeOptions
{
    public const string Position = "Node";

    public string PrivateKey { get; set; } = string.Empty;

    public string BlocksyncSeedPeer { get; init; } = string.Empty;

    public string ConsensusSeedPeer { get; init; } = string.Empty;
}
