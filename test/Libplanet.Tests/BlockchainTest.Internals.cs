using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Tests;

public partial class BlockchainTest
{
    [Fact]
    public void ListStagedTransactions()
    {
        var random = new System.Random(-873953735);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var repository = new Repository();
        var blockchainA = new Blockchain(genesisBlock, repository);

        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var signerC = RandomUtility.Signer(random);
        var signerD = RandomUtility.Signer(random);
        var signerE = RandomUtility.Signer(random);
        ISigner[] signers = [signerA, signerB, signerC, signerD, signerE];

        // A normal case and corner cases:
        // A. Normal case (3 txs: 0, 1, 2)
        // B. Nonces are out of order (2 txs: 1, 0)
        // C. Smaller nonces have later timestamps (2 txs: 0 (later), 1)
        // D. Some nonce numbers are missed out (3 txs: 0, 1, 3)
        // E. Reused nonces (4 txs: 0, 1, 1, 2)
        blockchainA.StagedTransactions.Add(signerA);
        var currentTime = DateTimeOffset.UtcNow;
        blockchainA.StagedTransactions.Add(signerB, new()
        {
            Nonce = 1,
            Timestamp = currentTime + TimeSpan.FromHours(1)
        });
        blockchainA.StagedTransactions.Add(signerC, new()
        {
            Nonce = 0L,
            Timestamp = DateTimeOffset.UtcNow + TimeSpan.FromHours(1),
        });
        blockchainA.StagedTransactions.Add(signerD, new()
        {
            Nonce = 0L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerE, new()
        {
            Nonce = 0L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerA);
        blockchainA.StagedTransactions.Add(signerB, new()
        {
            Nonce = 0L,
            Timestamp = currentTime,
        });
        blockchainA.StagedTransactions.Add(signerC, new()
        {
            Nonce = 1L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerD, new()
        {
            Nonce = 1L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerE, new()
        {
            Nonce = 1L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerD, new()
        {
            Nonce = 3L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerE, new()
        {
            Nonce = 1L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerE, new()
        {
            Nonce = 2L,
            Timestamp = DateTimeOffset.UtcNow,
        });
        blockchainA.StagedTransactions.Add(signerA);

        var stagedTransactionsA = blockchainA.StagedTransactions.Collect();

        // List is ordered by nonce.
        foreach (var signer in signers)
        {
            var signerTxs = stagedTransactionsA
                .Where(tx => tx.Signer == signer.Address)
                .ToImmutableList();
            Assert.Equal(signerTxs, signerTxs.OrderBy(tx => tx.Nonce));
        }

        var comparer = Comparer<Transaction>.Create((tx1, tx2) =>
        {
            if (tx1.Signer == tx2.Signer)
            {
                return tx1.Nonce.CompareTo(tx2.Nonce);
            }

            return tx1.Signer == signerA.Address ? -1 : 1;
        });
        var optionsB = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Sorter = items => items.OrderBy(tx => tx, comparer),
            },
        };
        var blockchainB = new Blockchain(repository, optionsB);

        // A is prioritized over B, C, D, E:
        var stagedTransactionsB = blockchainB.StagedTransactions.Collect();

        foreach (var tx in stagedTransactionsB.Take(3))
        {
            Assert.True(tx.Signer == signerA.Address);
        }

        // List is ordered by nonce.
        foreach (var signer in signers)
        {
            var signerTxs = stagedTransactionsB
                .Where(tx => tx.Signer.Equals(signer))
                .ToImmutableList();
            Assert.Equal(signerTxs, signerTxs.OrderBy(tx => tx.Nonce));
        }
    }

    [Fact]
    public void ExecuteActions()
    {
        var random = RandomUtility.GetRandom(_output);
        var signers = RandomUtility.Array(random, RandomUtility.Signer, 5);
        var addresses = signers.Select(s => s.Address).ToArray();
        var signer = RandomUtility.Signer(random);

        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var repository = new Repository();
        var options = new BlockchainOptions
        {
            SystemAction = new SystemAction
            {
                LeaveBlockActions = [new MinerReward(1)],
            },
        };
        var blockchain = new Blockchain(genesisBlock, repository, options);

        Transaction[] txs =
        [
            new TransactionBuilder
            {
                Nonce = 0L,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions =
                [
                    DumbAction.Create((addresses[0], "foo"), (null, addresses[0], 100)),
                    DumbAction.Create((addresses[1], "bar"), (null, addresses[1], 100)),
                ],
            }.Create(signer),
            new TransactionBuilder
            {
                Nonce = 1L,
                GenesisBlockHash = genesisBlock.BlockHash,
                Actions =
                [
                    DumbAction.Create((addresses[2], "baz"), (null, addresses[2], 100)),
                    DumbAction.Create((addresses[3], "qux"), (null, addresses[3], 100)),
                ],
            }.Create(signer),
        ];

        blockchain.StagedTransactions.AddRange(txs);
        blockchain.ProposeAndAppend(proposer);

        var minerAddress = proposer.Address;

        var expectedStates = new Dictionary<Address, object>
        {
            { addresses[0], "foo" },
            { addresses[1], "bar" },
            { addresses[2], "baz" },
            { addresses[3], "qux" },
            { minerAddress, 2 },
            { MinerReward.RewardRecordAddress, $"{minerAddress},{minerAddress}" },
        };

        foreach (var (key, value) in expectedStates)
        {
            var actualValue = blockchain.GetWorld().GetAccount(SystemAddresses.SystemAccount).GetValue(key);
            Assert.Equal(value, actualValue);
        }
    }

    [Fact]
    public void UpdateTxExecutions()
    {
        var random = RandomUtility.GetRandom(_output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var repository = new Repository();
        var blockchain = new Blockchain(genesisBlock, repository);

        var blockHash1 = RandomUtility.BlockHash(random);
        var blockHash2 = RandomUtility.BlockHash(random);
        var txId1 = RandomUtility.TxId(random);
        var txId2 = RandomUtility.TxId(random);

        Assert.Null(blockchain.TxExecutions.GetValueOrDefault(txId1));
        Assert.Null(blockchain.TxExecutions.GetValueOrDefault(txId2));

        var inputA = new TransactionExecutionInfo
        {
            BlockHash = blockHash1,
            TxId = txId1,
            EnterState = RandomUtility.HashDigest<SHA256>(random),
            LeaveState = RandomUtility.HashDigest<SHA256>(random),
            ExceptionNames = [],
        };
        var inputB = new TransactionExecutionInfo
        {
            BlockHash = blockHash1,
            TxId = txId2,
            EnterState = RandomUtility.HashDigest<SHA256>(random),
            LeaveState = RandomUtility.HashDigest<SHA256>(random),
            ExceptionNames = ["AnExceptionName"],
        };
        var inputC = new TransactionExecutionInfo
        {
            BlockHash = blockHash2,
            TxId = txId1,
            EnterState = RandomUtility.HashDigest<SHA256>(random),
            LeaveState = RandomUtility.HashDigest<SHA256>(random),
            ExceptionNames = ["AnotherExceptionName", "YetAnotherExceptionName"],
        };
        repository.TxExecutions.AddRange([inputA, inputB]);
        Assert.Throws<ArgumentException>(() => repository.TxExecutions.Add(inputC));

        Assert.Equal(inputA, blockchain.TxExecutions[inputA.TxId]);
        Assert.Equal(inputB, blockchain.TxExecutions[inputB.TxId]);
    }
}
