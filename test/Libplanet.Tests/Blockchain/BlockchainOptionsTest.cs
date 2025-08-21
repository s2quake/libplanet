using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Libplanet.TestUtilities;

namespace Libplanet.Tests.Blockchain;

public class BlockchainOptionsTest(ITestOutputHelper output) : IDisposable
{
    // private readonly ITestOutputHelper _output;

    // private readonly RepositoryFixture _fx;
    // private readonly Libplanet.Blockchain _chain;
    // private readonly BlockchainOptions _policy;


    public void Dispose()
    {
        // _fx.Dispose();
    }

    [Fact]
    public void Constructors()
    {
        var second10 = new TimeSpan(0, 0, 10);
        var optionsA = new BlockchainOptions
        {
            BlockInterval = second10,
        };
        Assert.Equal(second10, optionsA.BlockInterval);

        var optionsB = new BlockchainOptions
        {
            BlockInterval = TimeSpan.FromMilliseconds(65000),
        };
        Assert.Equal(
            new TimeSpan(0, 1, 5),
            optionsB.BlockInterval);

        var c = new BlockchainOptions();
        Assert.Equal(
            new TimeSpan(0, 0, 5),
            c.BlockInterval);
    }

    [Fact]
    public void ValidateNextBlockTx()
    {
        var random = RandomUtility.GetRandom(output);
        var validSigner = RandomUtility.Signer(random);
        var invalidSigner = RandomUtility.Signer(random);

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validSigner.Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

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
        var blockchain = TestUtils.MakeBlockchain(options: options);

        // Valid Transaction

        var validTx = new TransactionBuilder
        {
            Blockchain = blockchain,
        }.Create(validSigner);
        blockchain.StagedTransactions.Add(validTx);
        options.TransactionOptions.Validate(validTx);

        // Invalid Transaction
        var invalidTx = new TransactionBuilder
        {
            Blockchain = blockchain,
        }.Create(invalidSigner);
        blockchain.StagedTransactions.Add(invalidTx);
        options.TransactionOptions.Validate(invalidTx);
    }

    [Fact]
    public void ValidateNextBlockTxWithInnerException()
    {
        var random = RandomUtility.GetRandom(output);
        var validSigner = RandomUtility.Signer(random);
        var invalidSigner = RandomUtility.Signer(random);

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validSigner.Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        //Invalid Transaction with inner-exception
        void IsSignerValidWithInnerException(Transaction tx)
        {
            var validAddress = validSigner.Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException(
                    "invalid signer",
                    new InvalidOperationException("Invalid Signature"));
            }
        }

        // Invalid Transaction without Inner-exception
        var optionsA = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(IsSignerValid),
                ],
            },
        };
        var blockchainA = TestUtils.MakeBlockchain(options: optionsA);

        var invalidTx = new TransactionBuilder
        {
            Blockchain = blockchainA,
        }.Create(invalidSigner);
        blockchainA.StagedTransactions.Add(invalidTx);
        optionsA.TransactionOptions.Validate(invalidTx);

        // Invalid Transaction with Inner-exception.
        var optionsB = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(IsSignerValidWithInnerException),
                ],
            },
        };
        var blockchainB = TestUtils.MakeBlockchain(options: optionsA);

        invalidTx = new TransactionBuilder
        {
            Blockchain = blockchainA,
        }.Create(invalidSigner);
        blockchainA.StagedTransactions.Add(invalidTx);
    }

    [Fact]
    public void GetMinTransactionsPerBlock()
    {
        const int policyLimit = 2;

        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MinTransactionsPerBlock = policyLimit,
            },
        };
        var privateKey = new PrivateKey();
        var chain = TestUtils.MakeBlockchain(options);

        var tx = new TransactionBuilder
        {
            Blockchain = chain,
        }.Create(privateKey);
        chain.StagedTransactions.Add(tx);
        Assert.Single(chain.StagedTransactions.Collect());

        // Tests if MineBlock() method will throw an exception if less than the minimum
        // transactions are present
        Assert.Throws<OperationCanceledException>(
            () => chain.ProposeBlock(new PrivateKey()));
    }

    [Fact]
    public void GetMaxTransactionsPerBlock()
    {
        const int generatedTxCount = 10;
        const int policyLimit = 5;

        var store = new Libplanet.Data.Repository(new MemoryDatabase());
        var stateStore = new StateIndex();
        var policy = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxTransactionsPerBlock = policyLimit,
            },
        };
        var privateKey = new PrivateKey();
        var chain = TestUtils.MakeBlockchain(policy);

        _ = Enumerable
                .Range(0, generatedTxCount)
                .Select(_ =>
                {
                    var tx = new TransactionBuilder
                    {
                        Blockchain = chain,
                    }.Create(privateKey);
                    chain.StagedTransactions.Add(tx);
                    return tx;
                })
                .ToList();
        Assert.Equal(generatedTxCount, chain.StagedTransactions.Collect().Count);

        var block = chain.ProposeBlock(privateKey);
        Assert.Equal(policyLimit, block.Transactions.Count);
    }

    [Fact]
    public void GetMaxTransactionsPerSignerPerBlock()
    {
        const int keyCount = 2;
        const int generatedTxCount = 10;
        const int policyLimit = 4;

        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxTransactionsPerSignerPerBlock = policyLimit,
            },
        };
        var privateKeys = Enumerable.Range(0, keyCount).Select(_ => new PrivateKey()).ToList();
        var minerKey = privateKeys.First();
        var chain = TestUtils.MakeBlockchain(options);

        privateKeys.ForEach(
            key => _ = Enumerable
                .Range(0, generatedTxCount)
                .Select(_ =>
                {
                    var tx = new TransactionBuilder
                    {
                        Blockchain = chain,
                    }.Create(key);
                    chain.StagedTransactions.Add(tx);
                    return tx;
                })
                .ToList());
        Assert.Equal(generatedTxCount * keyCount, chain.StagedTransactions.Collect().Count);

        var block = chain.ProposeBlock(minerKey);
        Assert.Equal(policyLimit * keyCount, block.Transactions.Count);
        privateKeys.ForEach(
            key => Assert.Equal(
                policyLimit,
                block.Transactions.Count(tx => tx.Signer.Equals(key.Address))));
    }
}
