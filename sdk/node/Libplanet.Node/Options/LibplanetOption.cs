namespace Libplanet.Node.Options;

public sealed class LibplanetOption
{
    public StoreOptions Store { get; set; } = new StoreOptions();

    public SeedOptions BlocksyncSeed { get; set; } = new SeedOptions();

    public SeedOptions ConsensusSeed { get; set; } = new SeedOptions();

    public GenesisOptions Genesis { get; set; } = new GenesisOptions();

    public NodeOptions Node { get; set; } = new NodeOptions();
}
