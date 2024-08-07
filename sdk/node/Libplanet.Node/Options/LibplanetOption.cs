using System.ComponentModel;

namespace Libplanet.Node.Options;

public sealed class LibplanetOption
{
    [Description("The options for the store.")]
    public StoreOptions Store { get; set; } = new StoreOptions();

    [Description("The options for the block sync.")]
    public SeedOptions BlocksyncSeed { get; set; } = new SeedOptions();

    [Description("The options for the consensus.")]
    public SeedOptions ConsensusSeed { get; set; } = new SeedOptions();

    [Description("The options for creating a genesis block.")]
    public GenesisOptions Genesis { get; set; } = new GenesisOptions();

    [Description("The options for the node.")]
    public NodeOptions Node { get; set; } = new NodeOptions();
}
