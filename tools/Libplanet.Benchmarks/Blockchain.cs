using BenchmarkDotNet.Attributes;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Benchmarks;

public class Blockchain
{
    private Libplanet.Blockchain? _blockchain;

    [IterationSetup(Target = nameof(ContainsBlock))]
    public void SetupChain()
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
        _blockchain = new Libplanet.Blockchain(genesisBlock);
        for (var i = 0; i < 500; i++)
        {
            var signer = Rand.Signer(random);
            _blockchain.ProposeAndAppend(signer, validators);
        }
    }

    [Benchmark]
    public void ContainsBlock()
    {
        _blockchain?.Blocks.ContainsKey(_blockchain.Tip.BlockHash);
    }
}
