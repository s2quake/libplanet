using BenchmarkDotNet.Attributes;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Benchmarks;

public class Serialization
{
    private readonly Block _block;

    public Serialization()
    {
        var seed = Random.Shared.Next();
        var random = new Random(seed);
        var proposer = Rand.Signer(random);
        var validators = Rand.ImmutableSortedSet(
            random,
            Rand.TestValidator,
            10);
        var genesisBlock = new GenesisBlockBuilder
        {
            Validators = [.. validators.Select(v => (Validator)v)],
        }.Create(proposer);
        _block = genesisBlock;
    }

    [Benchmark]
    public void Serialize()
    {
        ModelSerializer.SerializeToBytes(_block);
    }
}
