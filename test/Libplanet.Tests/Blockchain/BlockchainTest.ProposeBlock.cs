using Libplanet.Data;
using Libplanet.Extensions;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
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
            PreviousBlockHash = blockchain.Tip.BlockHash,
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
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        var validAddress = validSigner.Address;
                        if (tx.Signer != validAddress && tx.Signer != proposer.Address)
                        {
                            throw new InvalidOperationException("invalid signer");
                        }
                    }),
                ],
            },
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);
        var validTx = blockchain.StagedTransactions.Add(validSigner, new());
        var invalidTx = blockchain.StagedTransactions.Add(invalidSigner, new());
        var (block, _) = blockchain.ProposeAndAppend(proposer);

        Assert.Contains(validTx, block.Transactions);
        Assert.DoesNotContain(invalidTx, block.Transactions);

        var invalidTxId = Assert.Single(blockchain.StagedTransactions.Keys);
        Assert.Equal(invalidTx.Id, invalidTxId);
    }

    [Fact]
    public void ProposeBlockWithReverseNonces()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var txs = new[]
        {
            new TransactionBuilder
            {
                Nonce = 2L,
                GenesisBlockHash = genesisBlock.BlockHash,
            }.Create(signer),
            new TransactionBuilder
            {
                Nonce = 1L,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = [],
            }.Create(signer),
            new TransactionBuilder
            {
                Nonce = 0L,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = [],
            }.Create(signer),
        };
        blockchain.StagedTransactions.AddRange(txs);
        var block = blockchain.Propose(RandomUtility.Signer(random));
        Assert.Equal(txs.Length, block.Transactions.Count);
    }

    [Fact]
    public void ProposeBlockWithLowerNonces()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        blockchain.StagedTransactions.Add(signer, new()
        {
            Nonce = 0L,
        });

        _ = blockchain.ProposeAndAppend(RandomUtility.Signer(random));

        // Trying to propose with lower nonce (0) than expected.
        blockchain.StagedTransactions.Add(signer, new()
        {
            Nonce = 0L,
        });

        var (block2, _) = blockchain.ProposeAndAppend(RandomUtility.Signer(random));

        Assert.Empty(block2.Transactions);
        Assert.Empty(blockchain.StagedTransactions.Collect());
        Assert.Single(blockchain.StagedTransactions);
    }

    [Fact]
    public void ProposeBlockWithBlockAction()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer1 = RandomUtility.Signer(random);
        var address1 = signer1.Address;

        var signer2 = RandomUtility.Signer(random);
        var address2 = signer2.Address;

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [DumbAction.Create((address1, "foo"))],
            },
        };
        var blockchain = new Libplanet.Blockchain(genesisBlock, options);

        var tx = blockchain.CreateTransaction(signer2, new TransactionParams
        {
            Actions = [DumbAction.Create((address2, "baz"))],
        });
        blockchain.StagedTransactions.Add(tx);
        blockchain.ProposeAndAppend(signer1);

        var value1 = blockchain.GetWorld().GetAccount(SystemAccount).GetValue(address1);
        var value2 = blockchain.GetWorld().GetAccount(SystemAccount).GetValue(address2);

        Assert.Equal(0, blockchain.GetNextTxNonce(address1));
        Assert.Equal(1, blockchain.GetNextTxNonce(address2));
        Assert.Equal("foo,foo", value1);
        Assert.Equal("baz", value2);

        blockchain.StagedTransactions.Add(signer1, @params: new()
        {
            Actions = [DumbAction.Create((address1, "bar"))],
        });
        blockchain.ProposeAndAppend(signer1);

        var value3 = blockchain.GetWorld().GetAccount(SystemAccount).GetValue(address1);
        var value4 = blockchain.GetWorld().GetAccount(SystemAccount).GetValue(address2);

        Assert.Equal(1, blockchain.GetNextTxNonce(address1));
        Assert.Equal(1, blockchain.GetNextTxNonce(address2));
        Assert.Equal("foo,foo,bar,foo", value3);
        Assert.Equal("baz", value4);
    }

    [Fact]
    public void ProposeBlockWithTxPriority()
    {
        var random = RandomUtility.GetRandom(_output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var signerC = RandomUtility.Signer(random);
        var addressA = signerA.Address; // Rank 0
        var addressB = signerB.Address; // Rank 1

        int Rank(Address address)
        {
            if (address == addressA)
            {
                return 0;
            }

            return address == addressB ? 1 : 2;
        }
        var comparer = Comparer<Transaction>.Create((tx1, tx2) => Rank(tx1.Signer).CompareTo(Rank(tx2.Signer)));
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Sorter = txs => txs.OrderBy(tx => tx, comparer).ThenBy(tx => tx.Nonce),
            },
        };

        var blockchain = new Libplanet.Blockchain(genesisBlock, options);

        var txsA = Enumerable.Range(0, 50)
            .Select(nonce => blockchain.CreateTransaction(signerA, new() { Nonce = nonce }))
            .ToArray();
        var txsB = Enumerable.Range(0, 60)
            .Select(nonce => blockchain.CreateTransaction(signerB, new() { Nonce = nonce }))
            .ToArray();
        var txsC = Enumerable.Range(0, 40)
            .Select(nonce => blockchain.CreateTransaction(signerC, new() { Nonce = nonce }))
            .ToArray();

        var txs = RandomUtility.Shuffle(random, txsA.Concat(txsB).Concat(txsC)).ToArray();
        blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs.Length, blockchain.StagedTransactions.Count);

        var block = blockchain.Propose(RandomUtility.Signer(random));
        Assert.Equal(100, block.Transactions.Count);
        Assert.Equal(
            txsA.Concat(txsB.Take(50)).Select(tx => tx.Id).ToImmutableSortedSet(),
            [.. block.Transactions.Select(tx => tx.Id)]);
    }

    [Fact]
    public void ProposeBlockWithLastCommit()
    {
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 3);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        _ = blockchain.ProposeAndAppend(RandomUtility.Signer(random));
        var votes = signers.Select(signer => new VoteMetadata
        {
            Height = blockchain.Tip.Height,
            Round = 0,
            BlockHash = blockchain.Tip.BlockHash,
            Timestamp = DateTimeOffset.UtcNow,
            Validator = signer.Address,
            ValidatorPower = BigInteger.One,
            Type = VoteType.PreCommit,
        }.Sign(signer)).ToImmutableArray();
        var blockCommit = new BlockCommit
        {
            Height = blockchain.Tip.Height,
            Round = 0,
            BlockHash = blockchain.Tip.BlockHash,
            Votes = votes,
        };
        var block = new BlockBuilder
        {
            Height = blockchain.Tip.Height + 1,
            PreviousBlockHash = blockchain.Tip.BlockHash,
            PreviousStateRootHash = blockchain.StateRootHash,
            PreviousBlockCommit = blockCommit,
        }.Create(RandomUtility.Signer(random));

        Assert.Equal(block.PreviousBlockCommit, blockCommit);
    }

    [Fact]
    public void IgnoreLowerNonceTxsAndPropose()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);

        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => blockchain.CreateTransaction(signer, new() { Nonce = nonce }))
            .ToArray();
        blockchain.StagedTransactions.AddRange(txsA);
        var (block1, _) = blockchain.ProposeAndAppend(RandomUtility.Signer(random));
        Assert.Equal(txsA, block1.Transactions);

        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => blockchain.CreateTransaction(signer, new() { Nonce = nonce }))
            .ToArray();
        blockchain.StagedTransactions.AddRange(txsB);

        // Propose only txs having higher or equal with nonce than expected nonce.
        var block2 = blockchain.Propose(RandomUtility.Signer(random));
        Assert.Single(block2.Transactions);
        Assert.Contains(txsB[3], block2.Transactions);
    }

    [Fact]
    public void IgnoreDuplicatedNonceTxs()
    {
        var random = RandomUtility.GetRandom(_output);
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var txs = Enumerable.Range(0, 3)
            .Select(_ => blockchain.CreateTransaction(signer, new() { Nonce = 0 }))
            .ToArray();
        blockchain.StagedTransactions.AddRange(txs);
        var (block, _) = blockchain.ProposeAndAppend(signer);

        Assert.Single(block.Transactions);
        Assert.Contains(block.Transactions.Single(), txs);
    }

    [Fact]
    public void GatherTransactionsToPropose()
    {
        var random = RandomUtility.GetRandom(_output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var signerC = RandomUtility.Signer(random);
        var addressA = signerA.Address;
        var addressB = signerB.Address;
        var addressC = signerC.Address;

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var optionsA = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxTransactions = 5,
                MaxTransactionsPerSigner = 3,
            },
        };
        var repository = new Repository();
        var blockchainA = new Libplanet.Blockchain(genesisBlock, repository, optionsA);

        var txsA = Enumerable.Range(0, 3)
            .Select(nonce => blockchainA.CreateTransaction(signerA, new() { Nonce = nonce }))
            .ToArray();
        var txsB = Enumerable.Range(0, 4)
            .Select(nonce => blockchainA.CreateTransaction(signerB, new() { Nonce = nonce }))
            .ToArray();
        var txsC = Enumerable.Range(0, 2)
            .Select(nonce => blockchainA.CreateTransaction(signerC, new() { Nonce = nonce }))
            .ToArray();
        var txs = RandomUtility.Shuffle(txsA.Concat(txsB).Concat(txsC)).ToArray();
        Assert.Empty(blockchainA.StagedTransactions.Collect());
        blockchainA.StagedTransactions.AddRange(txs);

        // Test if minTransactions and minTransactionsPerSigner work:
        var collectedTxsA = blockchainA.StagedTransactions.Collect();
        Assert.Equal(5, collectedTxsA.Length);
        var expectedNonces = new Dictionary<Address, long> { [addressA] = 0, [addressB] = 0, [addressC] = 0 };
        foreach (var tx in collectedTxsA)
        {
            var expectedNonce = expectedNonces[tx.Signer];
            Assert.True(expectedNonce < 3);
            Assert.Equal(expectedNonce, tx.Nonce);
            expectedNonces[tx.Signer] = expectedNonce + 1;
        }

        // Test if txPriority works:
        var comparer = Comparer<Transaction>.Create((tx1, tx2) =>
        {
#pragma warning disable S3358 // Ternary operators should not be nested
            var rank1 = tx1.Signer.Equals(addressA) ? 0 : (tx1.Signer.Equals(addressB) ? 1 : 2);
            var rank2 = tx2.Signer.Equals(addressA) ? 0 : (tx2.Signer.Equals(addressB) ? 1 : 2);
#pragma warning restore S3358 // Ternary operators should not be nested
            return rank1.CompareTo(rank2);
        });
        var optionsB = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxTransactions = 8,
                MaxTransactionsPerSigner = 3,
            },
            TransactionOptions = new TransactionOptions
            {
                Sorter = txs => txs.OrderBy(tx => tx, comparer).ThenBy(tx => tx.Nonce),
            },
        };
        var blockchainB = new Libplanet.Blockchain(repository, optionsB);
        var collectedTxsB = blockchainB.StagedTransactions.Collect();

        Assert.Equal(
            txsA.Concat(txsB.Take(3)).Concat(txsC).Select(tx => tx.Id).ToArray(),
            collectedTxsB.Select(tx => tx.Id).ToArray());
    }

    [Fact]
    public void MarkTransactionsToIgnoreWhileProposing()
    {
        var random = RandomUtility.GetRandom(_output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Libplanet.Blockchain(genesisBlock);
        var txWithInvalidAction = new TransactionMetadata
        {
            Nonce = 1L,
            Signer = signerB.Address,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = [new ActionBytecode([0x11])], // Invalid action
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(signerB);
        var txWithInvalidNonce = new TransactionMetadata
        {
            Nonce = 2L,
            Signer = signerB.Address,
            Timestamp = DateTimeOffset.UtcNow,
            GenesisBlockHash = genesisBlock.BlockHash,
            Actions = [],
        }.Sign(signerB);
        var txs = new[]
        {
            new TransactionMetadata
            {
                Nonce = 0L,
                Signer = signerA.Address,
                Timestamp = DateTimeOffset.UtcNow,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = [],
            }.Sign(signerA),
            new TransactionMetadata
            {
                Nonce = 1L,
                Signer = signerA.Address,
                Timestamp = DateTimeOffset.UtcNow,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = [],
            }.Sign(signerA),
            new TransactionMetadata
            {
                Nonce = 2,
                Signer = signerA.Address,
                Timestamp = DateTimeOffset.UtcNow,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = [],
            }.Sign(signerA),
            new TransactionMetadata
            {
                Nonce = 0,
                Signer = signerB.Address,
                Timestamp = DateTimeOffset.UtcNow,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions = [],
            }.Sign(signerB),
            txWithInvalidAction,
            txWithInvalidNonce,
        };

        // Invalid txs can be staged.
        blockchain.StagedTransactions.AddRange(txs);
        Assert.Equal(txs.Length, blockchain.StagedTransactions.Count);

        var block = blockchain.Propose(RandomUtility.Signer(random));

        Assert.DoesNotContain(txWithInvalidNonce, block.Transactions);
        Assert.DoesNotContain(txWithInvalidAction, block.Transactions);

        // txWithInvalidAction is marked ignored and removed
        Assert.Equal(txs.Length - 2, blockchain.StagedTransactions.Collect().Length);
        Assert.Contains(txWithInvalidNonce.Id, blockchain.StagedTransactions.Keys);
        Assert.Contains(txWithInvalidAction.Id, blockchain.StagedTransactions.Keys);
        blockchain.StagedTransactions.Prune();
        Assert.Equal(txs.Length - 1, blockchain.StagedTransactions.Count);
    }
}
