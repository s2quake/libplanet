using BenchmarkDotNet.Attributes;
using Libplanet.Types;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Actions;

namespace Libplanet.Benchmarks;

public class AppendBlock
{
    private readonly int _seed = Random.Shared.Next();
    private readonly Random _random;
    private readonly ISigner _proposer;
    private readonly ImmutableSortedSet<TestValidator> _validators;
    private readonly Block _genesisBlock;
    private readonly Libplanet.Blockchain _blockchain;
    private Block _block;
    private BlockCommit _blockCommit;

    public AppendBlock()
    {
        _random = new Random(_seed);
        _proposer = Rand.Signer(_random);
        _validators = Rand.ImmutableSortedSet(
            _random, Rand.TestValidator, Rand.Int32(_random, 4, 16));
        _genesisBlock = new GenesisBlockBuilder
        {
            Validators = [.. _validators.Select(v => (Validator)v)],
        }.Create(_proposer);
        _block = _genesisBlock;
        _blockchain = new Libplanet.Blockchain(_genesisBlock);
    }

    [IterationSetup(Target = nameof(AppendBlockOneTransactionNoAction))]
    public void PrepareAppendMakeOneTransactionNoAction()
    {
        _blockchain.StagedTransactions.Add(_proposer);
        PrepareAppend();
    }

    [IterationSetup(Target = nameof(AppendBlockTenTransactionsNoAction))]
    public void PrepareAppendMakeTenTransactionsNoAction()
    {
        for (var i = 0; i < 10; i++)
        {
            _blockchain.StagedTransactions.Add(Rand.Signer(_random));
        }

        PrepareAppend();
    }

    [IterationSetup(Target = nameof(AppendBlockOneTransactionWithActions))]
    public void PrepareAppendMakeOneTransactionWithActions()
    {
        var signer = Rand.Signer(_random);
        var address = signer.Address;
        var actions = new[]
        {
            DumbAction.Create((address, "foo")),
            DumbAction.Create((address, "bar")),
            DumbAction.Create((address, "baz")),
            DumbAction.Create((address, "qux")),
        };
        _blockchain.StagedTransactions.Add(signer, @params: new()
        {
            Actions = actions,
        });

        PrepareAppend();
    }

    [IterationSetup(Target = nameof(AppendBlockTenTransactionsWithActions))]
    public void PrepareAppendMakeTenTransactionsWithActions()
    {
        for (var i = 0; i < 10; i++)
        {
            var signer = Rand.Signer(_random);
            var address = signer.Address;
            var actions = new[]
            {
                DumbAction.Create((address, "foo")),
                DumbAction.Create((address, "bar")),
                DumbAction.Create((address, "baz")),
                DumbAction.Create((address, "qux")),
            };
            _blockchain.StagedTransactions.Add(signer, @params: new()
            {
                Actions = actions,
            });
        }

        PrepareAppend();
    }

    [Benchmark]
    public void AppendBlockOneTransactionNoAction()
    {
        _blockchain.Append(_block, _blockCommit);
    }

    [Benchmark]
    public void AppendBlockTenTransactionsNoAction()
    {
        _blockchain.Append(_block, _blockCommit);
    }

    [Benchmark]
    public void AppendBlockOneTransactionWithActions()
    {
        _blockchain.Append(_block, _blockCommit);
    }

    [Benchmark]
    public void AppendBlockTenTransactionsWithActions()
    {
        _blockchain.Append(_block, _blockCommit);
    }

    private void PrepareAppend()
    {
        _block = _blockchain.Propose(_proposer);
        _blockCommit = TestUtility.CreateBlockCommit(_block, _validators);
    }
}
