using System.ComponentModel;

namespace Libplanet.Node.Options;

public sealed class NodeOptions
{
    public const string Position = "Node";

    [Description("The private key of the node.")]
    public string PrivateKey { get; set; } = string.Empty;

    [Description("The endpoint of the node to block sync.")]
    public string BlocksyncSeedPeer { get; init; } = string.Empty;

    [Description("The endpoint of the node to consensus.")]
    public string ConsensusSeedPeer { get; init; } = string.Empty;
}
