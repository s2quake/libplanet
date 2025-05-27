using Libplanet.State;
using Libplanet.State.Tests.Common;
using Libplanet;
using Libplanet.Data;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;
using Xunit.Abstractions;

namespace Libplanet.Tests.Blockchain.Policies;

public class BlockPolicyTest : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly RepositoryFixture _fx;
    private readonly Libplanet.Blockchain _chain;
    private readonly BlockchainOptions _policy;

    public BlockPolicyTest(ITestOutputHelper output)
    {
        _fx = new MemoryRepositoryFixture();
        _output = output;
        _policy = new BlockchainOptions
        {
            BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
        };
        var repository = new Repository();
        _chain = new Libplanet.Blockchain(_fx.GenesisBlock, repository, _policy);
    }

    public void Dispose()
    {
        _fx.Dispose();
    }

    [Fact]
    public void Constructors()
    {
        var tenSec = new TimeSpan(0, 0, 10);
        var a = new BlockchainOptions
        {
            BlockInterval = tenSec,
        };
        Assert.Equal(tenSec, a.BlockInterval);

        var b = new BlockchainOptions
        {
            BlockInterval = TimeSpan.FromMilliseconds(65000),
        };
        Assert.Equal(
            new TimeSpan(0, 1, 5),
            b.BlockInterval);

        var c = new BlockchainOptions();
        Assert.Equal(
            new TimeSpan(0, 0, 5),
            c.BlockInterval);
    }

    [Fact]
    public void ValidateNextBlockTx()
    {
        var validKey = new PrivateKey();

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validKey.Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };

        // Valid Transaction

        var validTx = new TransactionBuilder
        {
        }.Create(validKey, _chain);
        _chain.StagedTransactions.Add(validTx);
        options.TransactionOptions.Validate(validTx);

        // Invalid Transaction
        var invalidKey = new PrivateKey();
        var invalidTx = new TransactionBuilder
        {
        }.Create(invalidKey, _chain);
        _chain.StagedTransactions.Add(invalidTx);
        options.TransactionOptions.Validate(invalidTx);
    }

    [Fact]
    public void ValidateNextBlockTxWithInnerException()
    {
        var validKey = new PrivateKey();
        var invalidKey = new PrivateKey();

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validKey.Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        //Invalid Transaction with inner-exception
        void IsSignerValidWithInnerException(Transaction tx)
        {
            var validAddress = validKey.Address;
            if (!tx.Signer.Equals(validAddress))
            {
                throw new InvalidOperationException(
                    "invalid signer",
                    new InvalidOperationException("Invalid Signature"));
            }
        }

        // Invalid Transaction without Inner-exception
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };

        var invalidTx = new TransactionBuilder
        {
        }.Create(invalidKey, _chain);
        _chain.StagedTransactions.Add(invalidTx);
        options.TransactionOptions.Validate(invalidTx);

        // Invalid Transaction with Inner-exception.
        options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValidWithInnerException),
            },
        };

        invalidTx = new TransactionBuilder
        {
        }.Create(invalidKey, _chain);
        _chain.StagedTransactions.Add(invalidTx);
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
        var chain = TestUtils.MakeBlockChain(options);

        var tx = new TransactionBuilder
        {
        }.Create(privateKey, chain);
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
        var stateStore = new StateStore();
        var policy = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxTransactionsPerBlock = policyLimit,
            },
        };
        var privateKey = new PrivateKey();
        var chain = TestUtils.MakeBlockChain(policy);

        _ = Enumerable
                .Range(0, generatedTxCount)
                .Select(_ =>
                {
                    var tx = new TransactionBuilder
                    {
                    }.Create(privateKey, chain);
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
        var chain = TestUtils.MakeBlockChain(options);

        privateKeys.ForEach(
            key => _ = Enumerable
                .Range(0, generatedTxCount)
                .Select(_ =>
                {
                    var tx = new TransactionBuilder
                    {
                    }.Create(key, chain);
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
