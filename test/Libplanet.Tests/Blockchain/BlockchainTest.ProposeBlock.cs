using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Tests.Store;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Blockchain;

public partial class BlockchainTest
{
    [Fact]
    public void ProposeBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
        };
        var maxActionBytes = options.BlockOptions.MaxActionBytes;
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);
        Assert.Single(blockchain.Blocks);
        Assert.Equal(
            $"{proposer.Address}",
            (string)blockchain.GetWorld().GetValue(SystemAccount, default));

        var proposerA = RandomUtility.Signer(random);
        var (block1, _) = blockchain.ProposeAndAppend(proposerA);

        Assert.True(blockchain.Blocks.ContainsKey(block1.BlockHash));
        Assert.Equal(2, blockchain.Blocks.Count);
        Assert.True(block1.GetActionByteLength() <= maxActionBytes);
        Assert.Equal(
            $"{proposer.Address},{proposerA.Address}",
            (string)blockchain.GetWorld().GetValue(SystemAccount, default));

        var proposerB = RandomUtility.Signer(random);
        var (block2, _) = blockchain.ProposeAndAppend(proposerB);
        Assert.True(blockchain.Blocks.ContainsKey(block2.BlockHash));
        Assert.Equal(3, blockchain.Blocks.Count);
        Assert.True(block2.GetActionByteLength() <= maxActionBytes);
        var expected1 = $"{proposer.Address},{proposerA.Address},{proposerB.Address}";
        Assert.Equal(
            expected1,
            (string)blockchain.GetWorld().GetAccount(SystemAccount).GetValue(default(Address)));

        var block3 = blockchain.Propose(RandomUtility.Signer(random));
        Assert.False(blockchain.Blocks.ContainsKey(block3.BlockHash));
        Assert.Equal(3, blockchain.Blocks.Count);
        Assert.True(block3.GetActionByteLength() <= maxActionBytes);
        var expected2 = $"{proposer.Address},{proposerA.Address},{proposerB.Address}";
        Assert.Equal(
            expected2,
            (string)blockchain.GetWorld().GetAccount(SystemAccount).GetValue(default(Address)));

        // Tests if ProposeBlock() method automatically fits the number of transactions
        // according to the right size.
        var manyActions = Enumerable.Repeat(DumbAction.Create((default, "_")), 200).ToArray();
        var signer = RandomUtility.Signer(random);
        for (var i = 0; i < 100; i++)
        {
            if (i % 25 == 0)
            {
                signer = RandomUtility.Signer(random);
            }

            blockchain.StagedTransactions.Add(signer, new()
            {
                Actions = manyActions,
            });
        }

        var block4 = blockchain.Propose(proposer: RandomUtility.Signer(random));
        Assert.False(blockchain.Blocks.ContainsKey(block4.BlockHash));
        Assert.True(block4.GetActionByteLength() <= maxActionBytes);
        Assert.Equal(8, block4.Transactions.Count);
        expected1 = $"{proposer.Address},{proposerA.Address},{proposerB.Address}";
        Assert.Equal(
            expected1,
            (string)blockchain.GetWorld().GetAccount(SystemAccount).GetValue(default(Address)));
    }

    [Fact]
    public void CanProposeInvalidGenesisBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new BlockBuilder
        {
            Transactions =
            [
                new TransactionBuilder
                {
                    Nonce = 5, // Invalid nonce,
                    Actions = [DumbAction.Create((RandomUtility.Signer(random).Address, "foo"))],
                }.Create(proposer),
            ]
        }.Create(proposer);
        Assert.Throws<ArgumentException>(() => new Libplanet.Blockchain(genesisBlock));
    }

    [Fact]
    public void CanProposeInvalidBlock()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var txSigner = RandomUtility.Signer(random);
        var tx = new TransactionBuilder
        {
            Nonce = 5,  // Invalid nonce
            GenesisBlockHash = blockchain.Genesis.BlockHash,
            Actions = [DumbAction.Create((RandomUtility.Signer(random).Address, "foo"))],
        }.Create(txSigner);

        blockchain.StagedTransactions.Add(tx);
        var block = new BlockBuilder
        {
            Height = 1,
            PreviousHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            Transactions = [tx],
        }.Create(proposer);
        var blockCommit = CreateBlockCommit(block);
        Assert.Throws<ArgumentException>(() => blockchain.Append(block, blockCommit));
    }

    [Fact]
    public void ProposeBlockWithPendingTxs()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);

        var signers = RandomUtility.Array(random, RandomUtility.Signer, 3);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var signerC = RandomUtility.Signer(random);
        var signerD = RandomUtility.Signer(random);
        var signerE = RandomUtility.Signer(random);
        var addressA = signerA.Address;
        var addressB = signerB.Address;
        var addressC = signerC.Address;
        var addressD = signerD.Address;
        var addressE = signerE.Address;

        var txs = new[]
        {
            new TransactionBuilder
            {
                Nonce = 0L,
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                Actions =
                [
                    DumbAction.Create((addressA, "1a")),
                    DumbAction.Create((addressB, "1b")),
                ],
            }.Create(signers[0]),
            new TransactionBuilder
            {
                Nonce = 1L,
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                Actions =
                [
                    DumbAction.Create((addressC, "2a")),
                    DumbAction.Create((addressD, "2b")),
                ],
            }.Create(signers[0]),

            // pending txs1
            new TransactionBuilder
            {
                Nonce = 1L,
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                Actions =
                [
                    DumbAction.Create((addressE, "3a")),
                    DumbAction.Create((addressA, "3b")),
                ],
            }.Create(signers[1]),
            new TransactionBuilder
            {
                Nonce = 2L,
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                Actions =
                [
                    DumbAction.Create((addressB, "4a")),
                    DumbAction.Create((addressC, "4b")),
                ],
            }.Create(signers[1]),

            // pending txs2
            new TransactionBuilder
            {
                Nonce = 0L,
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                Actions =
                [
                    DumbAction.Create((addressD, "5a")),
                    DumbAction.Create((addressE, "5b")),
                ],
            }.Create(signers[2]),
            new TransactionBuilder
            {
                Nonce = 2L,
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                Actions =
                [
                    DumbAction.Create((addressA, "6a")),
                    DumbAction.Create((addressB, "6b")),
                ],
            }.Create(signers[2]),
        };

        blockchain.StagedTransactions.AddRange(txs);

        Assert.Null(blockchain.GetWorld().GetAccount(SystemAccount).GetValueOrDefault(addressA));
        Assert.Null(blockchain.GetWorld().GetAccount(SystemAccount).GetValueOrDefault(addressB));
        Assert.Null(blockchain.GetWorld().GetAccount(SystemAccount).GetValueOrDefault(addressC));
        Assert.Null(blockchain.GetWorld().GetAccount(SystemAccount).GetValueOrDefault(addressD));
        Assert.Null(blockchain.GetWorld().GetAccount(SystemAccount).GetValueOrDefault(addressE));

        foreach (var tx in txs)
        {
            Assert.DoesNotContain(tx.Id, blockchain.TxExecutions);
        }

        var (block, _) = blockchain.ProposeAndAppend(signerA);

        Assert.True(blockchain.Blocks.ContainsKey(block.BlockHash));
        Assert.Contains(txs[0], block.Transactions);
        Assert.Contains(txs[1], block.Transactions);
        Assert.DoesNotContain(txs[2], block.Transactions);
        Assert.DoesNotContain(txs[3], block.Transactions);
        Assert.Contains(txs[4], block.Transactions);
        Assert.DoesNotContain(txs[5], block.Transactions);
        var txIds = blockchain.StagedTransactions.Keys.ToImmutableSortedSet();
        Assert.Contains(txs[2].Id, txIds);
        Assert.Contains(txs[3].Id, txIds);

        Assert.Equal(
            1,
            blockchain.GetWorld().GetAccount(SystemAccount).GetValue(addressA));
        Assert.Equal(
            "1b",
            blockchain.GetWorld().GetAccount(SystemAccount).GetValue(addressB));
        Assert.Equal(
            "2a",
            blockchain.GetWorld().GetAccount(SystemAccount).GetValue(addressC));
        Assert.IsType<string>(blockchain.GetWorld().GetAccount(SystemAccount).GetValue(addressD));
        Assert.Equal(
            new HashSet<string> { "2b", "5a" },
            [.. ((string)blockchain.GetWorld().GetAccount(SystemAccount).GetValue(addressD)).Split(',')]);
        Assert.Equal(
            "5b",
            blockchain.GetWorld().GetAccount(SystemAccount).GetValue(addressE));

        foreach (var tx in new[] { txs[0], txs[1], txs[4] })
        {
            var txExecution = blockchain.TxExecutions[tx.Id];
            Assert.False(txExecution.Fail);
            Assert.Equal(block.BlockHash, txExecution.BlockHash);
            Assert.Equal(tx.Id, txExecution.TxId);
        }
    }

    [Fact]
    public void ProposeBlockWithPolicyViolationTx()
    {
        var random = RandomUtility.GetRandom(_output);
        var validSigner = RandomUtility.Signer(random);
        var invalidSigner = RandomUtility.Signer(random);

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validSigner.Address;
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
                Validators =
                [
                    new RelayObjectValidator<Transaction>(IsSignerValid),
                ],
            },
        };
        var repository = new Repository();
        var blockchain = new Libplanet.Blockchain(fx.GenesisBlock, repository, options);

        var validTx = _blockchain.CreateTransaction(validSigner);
        _blockchain.StagedTransactions.Add(validTx);
        var invalidTx = _blockchain.CreateTransaction(invalidSigner);
        _blockchain.StagedTransactions.Add(invalidTx);

        var proposer = RandomUtility.Signer(random);
        var block = blockchain.Propose(proposer);
        blockchain.Append(block, CreateBlockCommit(block));

        var txs = block.Transactions.ToHashSet();

        Assert.Contains(validTx, txs);
        Assert.DoesNotContain(invalidTx, txs);

        Assert.Empty(blockchain.StagedTransactions.Keys);
    }

    [Fact]
    public void ProposeBlockWithReverseNonces()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = signer.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(signer),
            new TransactionMetadata
            {
                Nonce = 1,
                Signer = signer.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(signer),
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = signer.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(signer),
        };
        _blockchain.StagedTransactions.AddRange(txs);
        Block block = _blockchain.Propose(RandomUtility.Signer(random));
        Assert.Equal(txs.Length, block.Transactions.Count());
    }

    [Fact]
    public void ProposeBlockWithLowerNonces()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        _blockchain.StagedTransactions.AddRange(
            [
                new TransactionMetadata
                {
                    Nonce = 0,
                    Signer = signer.Address,
                    GenesisBlockHash = _blockchain.Genesis.BlockHash,
                    Actions = [],
                }.Sign(signer),
            ]);
        Block block1 = _blockchain.Propose(RandomUtility.Signer(random));
        _blockchain.Append(block1, CreateBlockCommit(block1));

        // Trying to propose with lower nonce (0) than expected.
        _blockchain.StagedTransactions.AddRange(
            new[]
            {
                new TransactionMetadata
                {
                    Nonce = 0,
                    Signer = signer.Address,
                    GenesisBlockHash = _blockchain.Genesis.BlockHash,
                    Actions = [],
                }.Sign(signer),
            });
        Block block2 = _blockchain.Propose(RandomUtility.Signer(random));
        _blockchain.Append(block2, CreateBlockCommit(block2));

        Assert.Empty(block2.Transactions);
        Assert.Empty(_blockchain.StagedTransactions.Collect());
        // Assert.Empty(_blockchain.StagedTransactions.Iterate(filtered: true));
        Assert.Single(_blockchain.StagedTransactions);
    }

    [Fact]
    public void ProposeBlockWithBlockAction()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer1 = RandomUtility.Signer(random);
        var address1 = signer1.Address;

        var signer2 = RandomUtility.Signer(random);
        var address2 = signer2.Address;

        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                BeginBlockActions = [],
                EndBlockActions = [DumbAction.Create((address1, "foo"))],
            },
        };
        var repository = new Repository();
        var blockchain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, options);

        var tx = blockchain.CreateTransaction(signer2, new TransactionParams
        {
            Actions = [DumbAction.Create((address2, "baz"))],
        });
        blockchain.StagedTransactions.Add(tx);
        var block = blockchain.Propose(signer1);
        blockchain.Append(block, CreateBlockCommit(block));

        var state1 = blockchain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address1);
        var state2 = blockchain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address2);

        Assert.Equal(0, blockchain.GetNextTxNonce(address1));
        Assert.Equal(1, blockchain.GetNextTxNonce(address2));
        Assert.Equal("foo,foo", state1);
        Assert.Equal("baz", state2);

        blockchain.StagedTransactions.Add(signer1, @params: new()
        {
            Actions = [DumbAction.Create((address1, "bar"))],
        });
        block = blockchain.Propose(signer1);
        blockchain.Append(block, CreateBlockCommit(block));

        state1 = blockchain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address1);
        state2 = blockchain
            .GetWorld()
            .GetAccount(SystemAccount)
            .GetValue(address2);

        Assert.Equal(1, blockchain.GetNextTxNonce(address1));
        Assert.Equal(1, blockchain.GetNextTxNonce(address2));
        Assert.Equal("foo,foo,bar,foo", state1);
        Assert.Equal("baz", state2);
    }

    [Fact]
    public void ProposeBlockWithTxPriority()
    {
        var random = RandomUtility.GetRandom(_output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var signerC = RandomUtility.Signer(random);
        Address a = signerA.Address; // Rank 0
        Address b = signerB.Address; // Rank 1
        Address c = signerC.Address; // Rank 2
        int Rank(Address address) => address.Equals(a) ? 0 : address.Equals(b) ? 1 : 2;
        Transaction[] txsA = Enumerable.Range(0, 50)
            .Select(nonce => _fx.MakeTransaction(nonce: nonce, signer: signerA))
            .ToArray();
        Transaction[] txsB = Enumerable.Range(0, 60)
            .Select(nonce => _fx.MakeTransaction(nonce: nonce, signer: signerB))
            .ToArray();
        Transaction[] txsC = Enumerable.Range(0, 40)
            .Select(nonce => _fx.MakeTransaction(nonce: nonce, signer: signerC))
            .ToArray();

        Transaction[] txs = [.. RandomUtility.Shuffle(random, txsA.Concat(txsB).Concat(txsC))];
        _blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs.Length, _blockchain.StagedTransactions.Collect().Length);

        IComparer<Transaction> txPriority =
            Comparer<Transaction>.Create((tx1, tx2) =>
                Rank(tx1.Signer).CompareTo(Rank(tx2.Signer)));
        Block block = _blockchain.Propose(RandomUtility.Signer(random));
        Assert.Equal(100, block.Transactions.Count);
        Assert.Equal(
            txsA.Concat(txsB.Take(50)).Select(tx => tx.Id).ToHashSet(),
            block.Transactions.Select(tx => tx.Id).ToHashSet());
    }

    [Fact]
    public void ProposeBlockWithLastCommit()
    {
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 3);
        var votes = signers.Select(signer => new VoteMetadata
        {
            Height = _blockchain.Tip.Height,
            Round = 0,
            BlockHash = _blockchain.Tip.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer)).ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = _blockchain.Tip.Height,
            Round = 0,
            BlockHash = _blockchain.Tip.BlockHash,
            Votes = votes,
        };
        Block block = _blockchain.Propose(RandomUtility.Signer(random));

        Assert.NotNull(block.PreviousCommit);
        Assert.Equal(block.PreviousCommit, blockCommit);
    }

    [Fact]
    public void IgnoreLowerNonceTxsAndPropose()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;
        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, signer: signer, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockchain.StagedTransactions.AddRange(txsA);
        Block b1 = _blockchain.Propose(RandomUtility.Signer(random));
        _blockchain.Append(b1, CreateBlockCommit(b1));
        Assert.Equal(txsA, b1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => _fx.MakeTransaction(
                nonce: nonce, signer: signer, timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockchain.StagedTransactions.AddRange(txsB);

        // Propose only txs having higher or equal with nonce than expected nonce.
        Block b2 = _blockchain.Propose(RandomUtility.Signer(random));
        Assert.Single(b2.Transactions);
        Assert.Contains(txsB[3], b2.Transactions);
    }

    [Fact]
    public void IgnoreDuplicatedNonceTxs()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var txs = Enumerable.Range(0, 3)
            .Select(_ => _fx.MakeTransaction(
                nonce: 0,
                signer: signer,
                timestamp: DateTimeOffset.Now))
            .ToArray();
        _blockchain.StagedTransactions.AddRange(txs);
        Block b = _blockchain.Propose(signer);
        _blockchain.Append(b, CreateBlockCommit(b));

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
    //     var keyA = RandomUtility.Signer(random);
    //     var keyB = RandomUtility.Signer(random);
    //     var keyC = RandomUtility.Signer(random);
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
    //     Assert.Empty(_blockchain.StagedTransactions.Collect(_blockchain.Options.BlockOptions));
    //     _blockchain.StagedTransactions.AddRange(txs);

    //     // Test if minTransactions and minTransactionsPerSigner work:
    //     var gathered = _blockchain.GatherTransactionsToPropose(1024 * 1024, 5, 3, 0);
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
    //     gathered = _blockchain.GatherTransactionsToPropose(1024 * 1024, 8, 3, 0, txPriority);
    //     Assert.Equal(
    //         txsA.Concat(txsB.Take(3)).Concat(txsC).Select(tx => tx.Id).ToArray(),
    //         gathered.Select(tx => tx.Id).ToArray());
    // }

    [Fact]
    public void MarkTransactionsToIgnoreWhileProposing()
    {
        var random = RandomUtility.GetRandom(_output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var txWithInvalidAction = new TransactionMetadata
        {
            Nonce = 1,
            Signer = signerB.Address,
            GenesisBlockHash = _blockchain.Genesis.BlockHash,
            Actions = [new ActionBytecode([0x11])], // Invalid action
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(signerB);
        Transaction txWithInvalidNonce = new TransactionMetadata
        {
            Nonce = 2,
            Signer = signerB.Address,
            GenesisBlockHash = _blockchain.Genesis.BlockHash,
            Actions = [],
        }.Sign(signerB);
        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = signerA.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = [],
            }.Sign(signerA),
            new TransactionMetadata
            {
                Nonce = 1,
                Signer = signerA.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = [],
            }.Sign(signerA),
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = signerA.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = [],
            }.Sign(signerA),
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = signerB.Address,
                GenesisBlockHash = _blockchain.Genesis.BlockHash,
                Actions = [],
            }.Sign(signerB),
            txWithInvalidAction,
            txWithInvalidNonce,
        };

        // Invalid txs can be staged.
        _blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs.Length, _blockchain.StagedTransactions.Collect().Length);

        var block = _blockchain.Propose(RandomUtility.Signer(random));

        Assert.DoesNotContain(txWithInvalidNonce, block.Transactions);
        Assert.DoesNotContain(txWithInvalidAction, block.Transactions);

        // txWithInvalidAction is marked ignored and removed
        Assert.Equal(txs.Length - 1, _blockchain.StagedTransactions.Collect().Length);
        Assert.DoesNotContain(txWithInvalidAction.Id, _blockchain.StagedTransactions.Keys);
    }
}
