using BenchmarkDotNet.Attributes;
using Libplanet.Types;
using Libplanet.TestUtilities.Actions;
using Libplanet.TestUtilities;

namespace Libplanet.Benchmarks;

public class ProposeBlock
{
    private readonly int _seed = Random.Shared.Next();
    private readonly Random _random;
    private readonly ISigner _proposer;
    private readonly ImmutableSortedSet<TestValidator> _validators;
    private readonly Block _genesisBlock;
    private readonly Libplanet.Blockchain _blockchain;
    private Block _block;
    private BlockCommit _blockCommit;

    public ProposeBlock()
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

    [IterationCleanup(
        Targets = new[]
        {
            nameof(ProposeBlockEmpty),
            nameof(ProposeBlockOneTransactionNoAction),
            nameof(ProposeBlockTenTransactionsNoAction),
            nameof(ProposeBlockOneTransactionWithActions),
            nameof(ProposeBlockTenTransactionsWithActions),
        })]
    public void CleanupPropose()
    {
        // To unstaging transactions, a block is appended to blockchain.
        _blockCommit = TestUtility.CreateBlockCommit(_block, _validators);
        _blockchain.Append(_block, _blockCommit);
    }

    [IterationSetup(Target = nameof(ProposeBlockOneTransactionNoAction))]
    public void MakeOneTransactionNoAction()
    {
        var tx = new TransactionBuilder
        {
        }.Create(_proposer, _blockchain);
        _blockchain.StagedTransactions.Add(tx);
    }

    [IterationSetup(Target = nameof(ProposeBlockTenTransactionsNoAction))]
    public void MakeTenTransactionsNoAction()
    {
        for (var i = 0; i < 10; i++)
        {
            var tx = new TransactionBuilder
            {
            }.Create(new PrivateKey().AsSigner(), _blockchain);
            _blockchain.StagedTransactions.Add(tx);
        }
    }

    [IterationSetup(Target = nameof(ProposeBlockOneTransactionWithActions))]
    public void MakeOneTransactionWithActions()
    {
        var signer = new PrivateKey().AsSigner();
        var address = signer.Address;
        var actions = new[]
        {
            DumbAction.Create((address, "foo")),
            DumbAction.Create((address, "bar")),
            DumbAction.Create((address, "baz")),
            DumbAction.Create((address, "qux")),
        };
        var tx = new TransactionBuilder
        {
            Actions = actions,
        }.Create(signer, _blockchain);
        _blockchain.StagedTransactions.Add(tx);
    }

    [IterationSetup(Target = nameof(ProposeBlockTenTransactionsWithActions))]
    public void MakeTenTransactionsWithActions()
    {
        for (var i = 0; i < 10; i++)
        {
            var signer = new PrivateKey().AsSigner();
            var address = signer.Address;
            var actions = new[]
            {
                DumbAction.Create((address, "foo")),
                DumbAction.Create((address, "bar")),
                DumbAction.Create((address, "baz")),
                DumbAction.Create((address, "qux")),
            };
            var tx = new TransactionBuilder
            {
                Actions = actions,
            }.Create(signer, _blockchain);
            _blockchain.StagedTransactions.Add(tx);
        }
    }

    [Benchmark]
    public void ProposeBlockEmpty()
    {
        _block = _blockchain.Propose(_proposer);
    }

    [Benchmark]
    public void ProposeBlockOneTransactionNoAction()
    {
        _block = _blockchain.Propose(_proposer);
    }

    [Benchmark]
    public void ProposeBlockTenTransactionsNoAction()
    {
        _block = _blockchain.Propose(_proposer);
    }

    [Benchmark]
    public void ProposeBlockOneTransactionWithActions()
    {
        _block = _blockchain.Propose(_proposer);
    }

    [Benchmark]
    public void ProposeBlockTenTransactionsWithActions()
    {
        _block = _blockchain.Propose(_proposer);
    }
}
