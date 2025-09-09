using System.ComponentModel;
using Libplanet.Node.DataAnnotations;

namespace Libplanet.Node.Options;

[Options(Position)]
public sealed class NodeOptions : OptionsBase<NodeOptions>
{
    public const string Position = "Node";

    [PrivateKey]
    [Description("The private key of Node.")]
    public string PrivateKey { get; set; } = string.Empty;

    [DnsEndPoint]
    public string EndPoint { get; set; } = string.Empty;

    [Peer]
    [Description("The endpoint of the node to block sync.")]
    public string BlocksyncSeedPeer { get; set; } = string.Empty;
}
