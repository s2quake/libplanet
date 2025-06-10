using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Tests.Store;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;
using static Libplanet.Tests.TestUtils;
using Random = System.Random;
using Libplanet.Types.Tests;
using Libplanet.TestUtilities;

namespace Libplanet.Tests.Blockchain;

public partial class BlockChainTest
{
    [Fact]
    public void ProposeBlock()
    {
        var maxTransactionsBytes = _blockChain.Options.BlockOptions.MaxTransactionsBytes;
        Assert.Single(_blockChain.Blocks);
        Assert.Equal(
            $"{GenesisProposer.Address}",
            (string)_blockChain.GetWorld().GetValue(SystemAccount, default));

        var proposerA = new PrivateKey();
        Block block = _blockChain.ProposeBlock(proposerA);
        _blockChain.Append(block, CreateBlockCommit(block));
        Assert.True(_blockChain.Blocks.ContainsKey(block.BlockHash));
        Assert.Equal(2, _blockChain.Blocks.Count);
        Assert.True(
            ModelSerializer.SerializeToBytes(block).Length <= maxTransactionsBytes);
        Assert.Equal(
            $"{GenesisProposer.Address},{proposerA.Address}",
            (string)_blockChain.GetWorld().GetValue(SystemAccount, default));

        var proposerB = new PrivateKey();
        Block anotherBlock = _blockChain.ProposeBlock(proposerB);
        _blockChain.Append(anotherBlock, CreateBlockCommit(anotherBlock));
        Assert.True(_blockChain.Blocks.ContainsKey(anotherBlock.BlockHash));
        Assert.Equal(3, _blockChain.Blocks.Count);
        Assert.True(
            ModelSerializer.SerializeToBytes(anotherBlock).Length <=
                maxTransactionsBytes);
        var expected = $"{GenesisProposer.Address},{proposerA.Address},{proposerB.Address}";
        Assert.Equal(
            expected,
            (string)_blockChain.GetWorld().GetAccount(SystemAccount).GetValue(default(Address)));

        Block block3 = _blockChain.ProposeBlock(new PrivateKey());
        Assert.False(_blockChain.Blocks.ContainsKey(block3.BlockHash));
        Assert.Equal(3, _blockChain.Blocks.Count);
        Assert.True(
            ModelSerializer.SerializeToBytes(block3).Length <= maxTransactionsBytes);
        expected = $"{GenesisProposer.Address},{proposerA.Address},{proposerB.Address}";
        Assert.Equal(
            expected,
            (string)_blockChain.GetWorld().GetAccount(SystemAccount).GetValue(default(Address)));

        // Tests if ProposeBlock() method automatically fits the number of transactions
        // according to the right size.
        DumbAction[] manyActions =
            Enumerable.Repeat(DumbAction.Create((default, "_")), 200).ToArray();
        PrivateKey? signer = null;
        int nonce = 0;
        for (int i = 0; i < 100; i++)
        {
            if (i % 25 == 0)
            {
                nonce = 0;
                signer = new PrivateKey();
            }

            Transaction heavyTx = _fx.MakeTransaction(
                manyActions,
                nonce: nonce,
                privateKey: signer);
            _blockChain.StagedTransactions.Add(heavyTx);
        }

        Block block4 = _blockChain.ProposeBlock(proposer: new PrivateKey());
        Assert.False(_blockChain.Blocks.ContainsKey(block4.BlockHash));
        _logger.Debug(
            $"{nameof(block4)}: {0} bytes",
            ModelSerializer.SerializeToBytes(block4).Length);
        _logger.Debug(
            $"{nameof(maxTransactionsBytes)}({nameof(block4)}.{nameof(block4.Height)}) = {0}",
            maxTransactionsBytes);
        Assert.True(
            ModelSerializer.SerializeToBytes(block4).Length <= maxTransactionsBytes);
        Assert.Equal(3, block4.Transactions.Count);
        expected = $"{GenesisProposer.Address},{proposerA.Address},{proposerB.Address}";
        Assert.Equal(
            expected,
            (string)_blockChain.GetWorld().GetAccount(SystemAccount).GetValue(default(Address)));
    }

    [Fact]
    public void CanProposeInvalidGenesisBlock()
    {
        using var fx = new MemoryRepositoryFixture();
        var options = fx.Options;
        var repository = fx.Repository;
        var genesisKey = new PrivateKey();
        var transaction = new TransactionBuilder
        {
            Nonce = 5, // Invalid nonce,
            Actions = [DumbAction.Create((new PrivateKey().Address, "foo"))],
        }.Create(genesisKey);
        var block = new RawBlock
        {
            Header = new BlockHeader
            {
                Height = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = genesisKey.Address,
            },
            Content = new BlockContent
            {
                Transactions = [transaction],
                Evidences = [],
            },
        }.Sign(genesisKey);
        var genesisBlock = new BlockBuilder
        {
            Transactions =
            [
                new TransactionMetadata
                {
                    Nonce = 5, // Invalid nonce,
                    Signer = genesisKey.Address,
                    Actions = (new[]
                    {
                        DumbAction.Create((new PrivateKey().Address, "foo")),
                    }).ToBytecodes(),
                }.Sign(genesisKey),
            ]
        }.Create(genesisKey);
        Assert.Throws<InvalidOperationException>(() => new Libplanet.Blockchain(genesisBlock, repository, options));
    }

    [Fact]
    public void CanProposeInvalidBlock()
    {
        using var fx = new MemoryRepositoryFixture();
        var options = fx.Options;
        var repository = new Repository();
        var blockChain = new Libplanet.Blockchain(fx.GenesisBlock, repository, options);
        var txKey = new PrivateKey();
        var tx = new TransactionMetadata
        {
            Nonce = 5,  // Invalid nonce
            Signer = txKey.Address,
            GenesisHash = _blockChain.Genesis.BlockHash,
            Actions = new[]
            {
                DumbAction.Create((new PrivateKey().Address, "foo")),
            }.ToBytecodes(),
        }.Sign(txKey);

        blockChain.StagedTransactions.Add(tx);
        var block = blockChain.ProposeBlock(new PrivateKey());
        var blockCommit = CreateBlockCommit(block);
        Assert.Throws<InvalidOperationException>(() => blockCommit);
    }

    [Fact]
    public void ProposeBlockWithPendingTxs()
    {
        var keys = new[] { new PrivateKey(), new PrivateKey(), new PrivateKey() };
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();
        var keyC = new PrivateKey();
        var keyD = new PrivateKey();
        var keyE = new PrivateKey();
        var addrA = keyA.Address;
        var addrB = keyB.Address;
        var addrC = keyC.Address;
        var addrD = keyD.Address;
        var addrE = keyE.Address;

        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = keys[0].Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = new[]
                {
                    DumbAction.Create((addrA, "1a")),
                    DumbAction.Create((addrB, "1b")),
                }.ToBytecodes(),
            }.Sign(keys[0]),
            new TransactionMetadata
            {
                Nonce = 1,
                Signer = keys[0].Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = new[]
                {
                    DumbAction.Create((addrC, "2a")),
                    DumbAction.Create((addrD, "2b")),
                }.ToBytecodes(),
            }.Sign(keys[0]),

            // pending txs1
            new TransactionMetadata
            {
                Nonce = 1,
                Signer = keys[1].Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = new[]
                {
                    DumbAction.Create((addrE, "3a")),
                    DumbAction.Create((addrA, "3b")),
                }.ToBytecodes(),
            }.Sign(keys[1]),
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = keys[1].Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = new[]
                {
                    DumbAction.Create((addrB, "4a")),
                    DumbAction.Create((addrC, "4b")),
                }.ToBytecodes(),
            }.Sign(keys[1]),

            // pending txs2
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = keys[2].Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = new[]
                {
                    DumbAction.Create((addrD, "5a")),
                    DumbAction.Create((addrE, "5b")),
                }.ToBytecodes(),
            }.Sign(keys[2]),
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = keys[2].Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = new[]
                {
                    DumbAction.Create((addrA, "6a")),
                    DumbAction.Create((addrB, "6b")),
                }.ToBytecodes(),
            }.Sign(keys[2]),
        };

        _blockChain.StagedTransactions.AddRange(txs);

        Assert.Null(_blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(addrA));
        Assert.Null(_blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(addrB));
        Assert.Null(_blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(addrC));
        Assert.Null(_blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(addrD));
        Assert.Null(_blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(addrE));

        foreach (Transaction tx in txs)
        {
            // Assert.Null(_blockChain.TxExecutions[tx.Id, _blockChain.Genesis.BlockHash]);
        }

        Block block = _blockChain.ProposeBlock(keyA);
        _blockChain.Append(block, CreateBlockCommit(block));

        Assert.True(_blockChain.Blocks.ContainsKey(block.BlockHash));
        Assert.Contains(txs[0], block.Transactions);
        Assert.Contains(txs[1], block.Transactions);
        Assert.DoesNotContain(txs[2], block.Transactions);
        Assert.DoesNotContain(txs[3], block.Transactions);
        Assert.Contains(txs[4], block.Transactions);
        Assert.DoesNotContain(txs[5], block.Transactions);
        var txIds = _blockChain.StagedTransactions.Keys.ToImmutableSortedSet();
        Assert.Contains(txs[2].Id, txIds);
        Assert.Contains(txs[3].Id, txIds);

        Assert.Equal(
            1,
            _blockChain
                .GetWorld()
                .GetAccount(SystemAccount)
                .GetValue(addrA));
        Assert.Equal(
            "1b",
            _blockChain
                .GetWorld()
                .GetAccount(SystemAccount)
                .GetValue(addrB));
        Assert.Equal(
            "2a",
            _blockChain
                .GetWorld()
                .GetAccount(SystemAccount)
                .GetValue(addrC));
        Assert.IsType<string>(
            _blockChain
                .GetWorld()
                .GetAccount(SystemAccount)
                .GetValue(addrD));
        Assert.Equal(
            new HashSet<string> { "2b", "5a" },
            ((string)_blockChain
                .GetWorld()
                .GetAccount(SystemAccount)
                .GetValue(addrD)).Split(new[] { ',' }).ToHashSet());
        Assert.Equal(
            "5b",
            _blockChain
                .GetWorld()
                .GetAccount(SystemAccount)
                .GetValue(addrE));

        foreach (Transaction tx in new[] { txs[0], txs[1], txs[4] })
        {
            TxExecution txx = _blockChain.TxExecutions[tx.Id];
            Assert.False(txx.Fail);
            Assert.Equal(block.BlockHash, txx.BlockHash);
            Assert.Equal(tx.Id, txx.TxId);
            // Assert.Null(_blockChain.TxExecutions[tx.Id, _blockChain.Genesis.BlockHash]);
        }
    }

    [Fact]
    public void ProposeBlockWithPolicyViolationTx()
    {
        var validKey = new PrivateKey();
        var invalidKey = new PrivateKey();

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validKey.Address;
            if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(_fx.Proposer.Address))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        using var fx = new MemoryRepositoryFixture();
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };
        var repository = new Repository();
        var blockChain = new Libplanet.Blockchain(fx.GenesisBlock, repository, options);

        var validTx = new TransactionBuilder
        {
        }.Create(validKey, _blockChain);
        _blockChain.StagedTransactions.Add(validTx);
        var invalidTx = new TransactionBuilder
        {
        }.Create(invalidKey, _blockChain);
        _blockChain.StagedTransactions.Add(invalidTx);

        var proposer = new PrivateKey();
        var block = blockChain.ProposeBlock(proposer);
        blockChain.Append(block, CreateBlockCommit(block));

        var txs = block.Transactions.ToHashSet();

        Assert.Contains(validTx, txs);
        Assert.DoesNotContain(invalidTx, txs);

        Assert.Empty(blockChain.StagedTransactions.Keys);
    }

    [Fact]
    public void ProposeBlockWithReverseNonces()
    {
        var key = new PrivateKey();
        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = key.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(key),
            new TransactionMetadata
            {
                Nonce = 1,
                Signer = key.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(key),
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = key.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(key),
        };
        _blockChain.StagedTransactions.AddRange(txs);
        Block block = _blockChain.ProposeBlock(new PrivateKey());
        Assert.Equal(txs.Length, block.Transactions.Count());
    }

    [Fact]
    public void ProposeBlockWithLowerNonces()
    {
        var key = new PrivateKey();
        _blockChain.StagedTransactions.AddRange(
            [
                new TransactionMetadata
                {
                    Nonce = 0,
                    Signer = key.Address,
                    GenesisHash = _blockChain.Genesis.BlockHash,
                    Actions = [],
                }.Sign(key),
            ]);
        Block block1 = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(block1, CreateBlockCommit(block1));

        // Trying to propose with lower nonce (0) than expected.
        _blockChain.StagedTransactions.AddRange(
            new[]
            {
                new TransactionMetadata
                {
                    Nonce = 0,
                    Signer = key.Address,
                    GenesisHash = _blockChain.Genesis.BlockHash,
                    Actions = [],
                }.Sign(key),
            });
        Block block2 = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(block2, CreateBlockCommit(block2));

        Assert.Empty(block2.Transactions);
        Assert.Empty(_blockChain.StagedTransactions.Collect());
        // Assert.Empty(_blockChain.StagedTransactions.Iterate(filtered: true));
        Assert.Single(_blockChain.StagedTransactions);
    }

    [Fact]
    public void ProposeBlockWithBlockAction()
    {
        var privateKey1 = new PrivateKey();
        var address1 = privateKey1.Address;

        var privateKey2 = new PrivateKey();
        var address2 = privateKey2.Address;

        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                BeginBlockActions = [],
                EndBlockActions = [DumbAction.Create((address1, "foo"))],
            },
        };
        var repository = new Repository();
        var blockChain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, options);

        var tx = new TransactionBuilder
        {
            Actions = [DumbAction.Create((address2, "baz"))],
        }.Create(privateKey2, blockChain);
        blockChain.StagedTransactions.Add(tx);
        var block = blockChain.ProposeBlock(privateKey1);
        blockChain.Append(block, CreateBlockCommit(block));

        var state1 = blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address1);
        var state2 = blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address2);

        Assert.Equal(0, blockChain.GetNextTxNonce(address1));
        Assert.Equal(1, blockChain.GetNextTxNonce(address2));
        Assert.Equal("foo,foo", state1);
        Assert.Equal("baz", state2);

        blockChain.StagedTransactions.Add(submission: new()
        {
            Signer = privateKey1,
            Actions = [DumbAction.Create((address1, "bar"))],
        });
        block = blockChain.ProposeBlock(privateKey1);
        blockChain.Append(block, CreateBlockCommit(block));

        state1 = blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address1);
        state2 = blockChain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address2);

        Assert.Equal(1, blockChain.GetNextTxNonce(address1));
        Assert.Equal(1, blockChain.GetNextTxNonce(address2));
        Assert.Equal("foo,foo,bar,foo", state1);
        Assert.Equal("baz", state2);
    }

    [Fact]
    public void ProposeBlockWithTxPriority()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();
        var keyC = new PrivateKey();
        Address a = keyA.Address; // Rank 0
        Address b = keyB.Address; // Rank 1
        Address c = keyC.Address; // Rank 2
        int Rank(Address address) => address.Equals(a) ? 0 : address.Equals(b) ? 1 : 2;
        Transaction[] txsA = Enumerable.Range(0, 50)
            .Select(nonce => _fx.MakeTransaction(nonce: nonce, privateKey: keyA))
            .ToArray();
        Transaction[] txsB = Enumerable.Range(0, 60)
            .Select(nonce => _fx.MakeTransaction(nonce: nonce, privateKey: keyB))
            .ToArray();
        Transaction[] txsC = Enumerable.Range(0, 40)
            .Select(nonce => _fx.MakeTransaction(nonce: nonce, privateKey: keyC))
            .ToArray();

        var random = new Random();
        Transaction[] txs = [.. RandomUtility.Shuffle(random, txsA.Concat(txsB).Concat(txsC))];
        _blockChain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs.Length, _blockChain.StagedTransactions.Collect().Count);

        IComparer<Transaction> txPriority =
            Comparer<Transaction>.Create((tx1, tx2) =>
                Rank(tx1.Signer).CompareTo(Rank(tx2.Signer)));
        Block block = _blockChain.ProposeBlock(new PrivateKey());
        Assert.Equal(100, block.Transactions.Count);
        Assert.Equal(
            txsA.Concat(txsB.Take(50)).Select(tx => tx.Id).ToHashSet(),
            block.Transactions.Select(tx => tx.Id).ToHashSet());
    }

    [Fact]
    public void ProposeBlockWithLastCommit()
    {
        var keys = Enumerable.Range(0, 3).Select(_ => new PrivateKey()).ToList();
        var votes = keys.Select(key => new VoteMetadata
        {
            Height = _blockChain.Tip.Height,
            Round = 0,
            BlockHash = _blockChain.Tip.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = key.Address,
            ValidatorPower = BigInteger.One,
            Flag = VoteFlag.PreCommit,
        }.Sign(key)).ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = _blockChain.Tip.Height,
            Round = 0,
            BlockHash = _blockChain.Tip.BlockHash,
            Votes = votes,
        };
        Block block = _blockChain.ProposeBlock(new PrivateKey());

        Assert.NotNull(block.PreviousCommit);
        Assert.Equal(block.PreviousCommit, blockCommit);
    }

    [Fact]
    public void IgnoreLowerNonceTxsAndPropose()
    {
        var privateKey = new PrivateKey();
        var address = privateKey.Address;
        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, privateKey: privateKey, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockChain.StagedTransactions.AddRange(txsA);
        Block b1 = _blockChain.ProposeBlock(new PrivateKey());
        _blockChain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(txsA, b1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, privateKey: privateKey, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockChain.StagedTransactions.AddRange(txsB);

        // Propose only txs having higher or equal with nonce than expected nonce.
        Block b2 = _blockChain.ProposeBlock(new PrivateKey());
        Assert.Single(b2.Transactions);
        Assert.Contains(txsB[3], b2.Transactions);
    }

    [Fact]
    public void IgnoreDuplicatedNonceTxs()
    {
        var privateKey = new PrivateKey();
        var txs = Enumerable.Range(0, 3)
            .Select(_ => _fx.MakeTransaction(
                nonce: 0,
                privateKey: privateKey,
                timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockChain.StagedTransactions.AddRange(txs);
        Block b = _blockChain.ProposeBlock(privateKey);
        _blockChain.Append(b, CreateBlockCommit(b));

        Assert.Single(b.Transactions);
        Assert.Contains(b.Transactions.Single(), txs);
    }

    // [Fact]
    // public void GatherTransactionsToPropose()
    // {
    //     // TODO: We test more properties of GatherTransactionsToPropose() method:
    //     //       - if transactions are cut off if they exceed GetMaxTransactionsBytes()
    //     //       - if transactions with already consumed nonces are excluded
    //     //       - if transactions with greater nonces than unconsumed nonces are excluded
    //     //       - if transactions are cut off if the process exceeds the timeout (4 sec)
    //     var keyA = new PrivateKey();
    //     var keyB = new PrivateKey();
    //     var keyC = new PrivateKey();
    //     Address a = keyA.Address;
    //     Address b = keyB.Address;
    //     Address c = keyC.Address;
    //     _logger.Verbose("Address {Name}: {Address}", nameof(a), a);
    //     _logger.Verbose("Address {Name}: {Address}", nameof(b), b);
    //     _logger.Verbose("Address {Name}: {Address}", nameof(c), c);

    //     Transaction[] txsA = Enumerable.Range(0, 3)
    //         .Select(nonce => _fx.MakeTransaction(nonce: nonce, privateKey: keyA))
    //         .ToArray();
    //     Transaction[] txsB = Enumerable.Range(0, 4)
    //         .Select(nonce => _fx.MakeTransaction(nonce: nonce, privateKey: keyB))
    //         .ToArray();
    //     Transaction[] txsC = Enumerable.Range(0, 2)
    //         .Select(nonce => _fx.MakeTransaction(nonce: nonce, privateKey: keyC))
    //         .ToArray();
    //     var random = new Random();
    //     Transaction[] txs =
    //         txsA.Concat(txsB).Concat(txsC).Shuffle(random).ToArray();
    //     Assert.Empty(_blockChain.StagedTransactions.Collect(_blockChain.Options.BlockOptions));
    //     _blockChain.StagedTransactions.AddRange(txs);

    //     // Test if minTransactions and minTransactionsPerSigner work:
    //     var gathered = _blockChain.GatherTransactionsToPropose(1024 * 1024, 5, 3, 0);
    //     Assert.Equal(5, gathered.Length);
    //     var expectedNonces = new Dictionary<Address, long> { [a] = 0, [b] = 0, [c] = 0 };
    //     foreach (Transaction tx in gathered)
    //     {
    //         long expectedNonce = expectedNonces[tx.Signer];
    //         Assert.True(expectedNonce < 3);
    //         Assert.Equal(expectedNonce, tx.Nonce);
    //         expectedNonces[tx.Signer] = expectedNonce + 1;
    //     }

    //     // Test if txPriority works:
    //     IComparer<Transaction> txPriority =
    //         Comparer<Transaction>.Create((tx1, tx2) =>
    //         {
    //             int rank1 = tx1.Signer.Equals(a) ? 0 : (tx1.Signer.Equals(b) ? 1 : 2);
    //             int rank2 = tx2.Signer.Equals(a) ? 0 : (tx2.Signer.Equals(b) ? 1 : 2);
    //             return rank1.CompareTo(rank2);
    //         });
    //     gathered = _blockChain.GatherTransactionsToPropose(1024 * 1024, 8, 3, 0, txPriority);
    //     Assert.Equal(
    //         txsA.Concat(txsB.Take(3)).Concat(txsC).Select(tx => tx.Id).ToArray(),
    //         gathered.Select(tx => tx.Id).ToArray());
    // }

    [Fact]
    public void MarkTransactionsToIgnoreWhileProposing()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();
        var txWithInvalidAction = new TransactionMetadata
        {
            Nonce = 1,
            Signer = keyB.Address,
            GenesisHash = _blockChain.Genesis.BlockHash,
            Actions = [new ActionBytecode([0x11])], // Invalid action
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(keyB);
        Transaction txWithInvalidNonce = new TransactionMetadata
        {
            Nonce = 2,
            Signer = keyB.Address,
            GenesisHash = _blockChain.Genesis.BlockHash,
            Actions = [],
        }.Sign(keyB);
        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = keyA.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = [],
            }.Sign(keyA),
            new TransactionMetadata
            {
                Nonce = 1,
                Signer = keyA.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = [],
            }.Sign(keyA),
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = keyA.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = [],
            }.Sign(keyA),
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = keyB.Address,
                GenesisHash = _blockChain.Genesis.BlockHash,
                Actions = [],
            }.Sign(keyB),
            txWithInvalidAction,
            txWithInvalidNonce,
        };

        // Invalid txs can be staged.
        _blockChain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs.Length, _blockChain.StagedTransactions.Collect().Count);

        var block = _blockChain.ProposeBlock(new PrivateKey());

        Assert.DoesNotContain(txWithInvalidNonce, block.Transactions);
        Assert.DoesNotContain(txWithInvalidAction, block.Transactions);

        // txWithInvalidAction is marked ignored and removed
        Assert.Equal(txs.Length - 1, _blockChain.StagedTransactions.Collect().Count);
        Assert.DoesNotContain(txWithInvalidAction.Id, _blockChain.StagedTransactions.Keys);
    }
}
