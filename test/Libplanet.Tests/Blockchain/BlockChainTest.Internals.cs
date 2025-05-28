using Libplanet.State;
using Libplanet.State.Tests.Common;
using Libplanet;
using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Tests.Blockchain;

public partial class BlockChainTest
{
    [Fact]
    public void ListStagedTransactions()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("XUNIT_UNITY_RUNNER") is null,
            "This test causes timeout");

        Transaction MkTx(PrivateKey key, long nonce, DateTimeOffset? ts = null) =>
            new TransactionMetadata
            {
                Nonce = nonce,
                Signer = key.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
                Timestamp = ts ?? DateTimeOffset.UtcNow,
            }.Sign(key);

        PrivateKey a = new PrivateKey();
        PrivateKey b = new PrivateKey();
        PrivateKey c = new PrivateKey();
        PrivateKey d = new PrivateKey();
        PrivateKey e = new PrivateKey();
        List<Address> signers = new List<Address>()
        {
            a.Address, b.Address, c.Address, d.Address, e.Address,
        };

        // A normal case and corner cases:
        // A. Normal case (3 txs: 0, 1, 2)
        // B. Nonces are out of order (2 txs: 1, 0)
        // C. Smaller nonces have later timestamps (2 txs: 0 (later), 1)
        // D. Some nonce numbers are missed out (3 txs: 0, 1, 3)
        // E. Reused nonces (4 txs: 0, 1, 1, 2)
        _blockChain.StagedTransactions.Add(new TransactionBuilder
        {
        }.Create(a, _blockChain));
        DateTimeOffset currentTime = DateTimeOffset.UtcNow;
        _blockChain.StagedTransactions.Add(MkTx(b, 1, currentTime + TimeSpan.FromHours(1)));
        _blockChain.StagedTransactions.Add(MkTx(c, 0, DateTimeOffset.UtcNow + TimeSpan.FromHours(1)));
        _blockChain.StagedTransactions.Add(MkTx(d, 0, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(MkTx(e, 0, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(new TransactionBuilder
        {
        }.Create(a, _blockChain));
        _blockChain.StagedTransactions.Add(MkTx(b, 0, currentTime));
        _blockChain.StagedTransactions.Add(MkTx(c, 1, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(MkTx(d, 1, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(MkTx(e, 1, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(MkTx(d, 3, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(MkTx(e, 1, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(MkTx(e, 2, DateTimeOffset.UtcNow));
        _blockChain.StagedTransactions.Add(new TransactionBuilder
        {
        }.Create(a, _blockChain));

        var stagedTransactions = _blockChain.StagedTransactions.Collect();

        // List is ordered by nonce.
        foreach (var signer in signers)
        {
            var signerTxs = stagedTransactions
                .Where(tx => tx.Signer.Equals(signer))
                .ToImmutableList();
            Assert.Equal(signerTxs, signerTxs.OrderBy(tx => tx.Nonce));
        }

        // A is prioritized over B, C, D, E:
        IComparer<Transaction> priority = Comparer<Transaction>.Create(
            (tx1, tx2) => tx1.Signer.Equals(a.Address) ? -1 : 1);
        stagedTransactions = _blockChain.StagedTransactions.Collect();

        foreach (var tx in stagedTransactions.Take(3))
        {
            Assert.True(tx.Signer.Equals(a.Address));
        }

        // List is ordered by nonce.
        foreach (var signer in signers)
        {
            var signerTxs = stagedTransactions
                .Where(tx => tx.Signer.Equals(signer))
                .ToImmutableList();
            Assert.Equal(signerTxs, signerTxs.OrderBy(tx => tx.Nonce));
        }
    }

    [Fact]
    public void ExecuteActions()
    {
        // (var addresses, Transaction[] txs) = MakeFixturesForAppendTests();
        // var genesis = _blockChain.Genesis;

        // Block block1 = _blockChain.ProposeBlock(
        //     _fx.Proposer,
        //     CreateBlockCommit(_blockChain.Tip),
        //     txs.ToImmutableSortedSet(),
        //     []);
        // _blockChain.Append(block1, CreateBlockCommit(block1), render: true);

        // var minerAddress = genesis.Proposer;

        // var expectedStates = new Dictionary<Address, object>
        // {
        //     { addresses[0], "foo" },
        //     { addresses[1], "bar" },
        //     { addresses[2], "baz" },
        //     { addresses[3], "qux" },
        //     { minerAddress, 2 },
        //     { MinerReward.RewardRecordAddress, $"{minerAddress},{minerAddress}" },
        // };


        // IValue legacyStateRootRaw =
        //     _blockChain.StateStore.GetStateRoot(_blockChain.GetNextStateRootHash() ?? default)
        //     [ToStateKey(ReservedAddresses.LegacyAccount)];
        // Assert.NotNull(legacyStateRootRaw);
        // var legacyStateRoot =
        //     new HashDigest<SHA256>(((Binary)legacyStateRootRaw).ByteArray);
        // foreach (KeyValuePair<Address, IValue> pair in expectedStates)
        // {
        //     AssertBencodexEqual(
        //         pair.Value,
        //         _blockChain.StateStore
        //             .GetStateRoot(legacyStateRoot)
        //             .GetMany(new[] { ToStateKey(pair.Key) })[0]);
        // }
    }

    // [SkippableTheory]
    // [InlineData(true)]
    // [InlineData(false)]
    // public void UpdateTxExecutions(bool getTxExecutionViaStore)
    // {
    //     void AssertTxExecutionEqual(TxExecution expected, TxExecution actual)
    //     {
    //         Assert.Equal(expected.Fail, actual.Fail);
    //         Assert.Equal(expected.TxId, actual.TxId);
    //         Assert.Equal(expected.BlockHash, actual.BlockHash);
    //         Assert.Equal(expected.InputState, actual.InputState);
    //         Assert.Equal(expected.OutputState, actual.OutputState);
    //         Assert.Equal(expected.ExceptionNames, actual.ExceptionNames);
    //     }

    //     var getTxExecution = new Func<BlockHash, TxId, TxExecution>(
    //         (blockHash, txId) => _blockChain.TxExecutions[txId]);

    //     Assert.Null(getTxExecution(_fx.Hash1, _fx.TxId1));
    //     Assert.Null(getTxExecution(_fx.Hash1, _fx.TxId2));
    //     Assert.Null(getTxExecution(_fx.Hash2, _fx.TxId1));
    //     Assert.Null(getTxExecution(_fx.Hash2, _fx.TxId2));

    //     var random = new System.Random();
    //     var inputA = new TxExecution
    //     {
    //         BlockHash = _fx.Hash1,
    //         TxId = _fx.TxId1,
    //         InputState = new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size)),
    //         OutputState = new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size)),
    //         ExceptionNames = [],
    //     };
    //     var inputB = new TxExecution
    //     {
    //         BlockHash = _fx.Hash1,
    //         TxId = _fx.TxId2,
    //         InputState = new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size)),
    //         OutputState = new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size)),
    //         ExceptionNames = ["AnExceptionName"],
    //     };
    //     var inputC = new TxExecution
    //     {
    //         BlockHash = _fx.Hash2,
    //         TxId = _fx.TxId1,
    //         InputState = new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size)),
    //         OutputState = new HashDigest<SHA256>(TestUtils.GetRandomBytes(HashDigest<SHA256>.Size)),
    //         ExceptionNames = ["AnotherExceptionName", "YetAnotherExceptionName"],
    //     };
    //     _blockChain.TxExecutions.AddRange([inputA, inputB, inputC]);

    //     AssertTxExecutionEqual(inputA, getTxExecution(_fx.Hash1, _fx.TxId1));
    //     AssertTxExecutionEqual(inputB, getTxExecution(_fx.Hash1, _fx.TxId2));
    //     AssertTxExecutionEqual(inputC, getTxExecution(_fx.Hash2, _fx.TxId1));
    //     Assert.Null(getTxExecution(_fx.Hash2, _fx.TxId2));
    // }
}
