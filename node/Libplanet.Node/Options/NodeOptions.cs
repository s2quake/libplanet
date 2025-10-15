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
}
