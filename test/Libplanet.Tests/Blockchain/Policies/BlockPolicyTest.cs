using Libplanet.Action;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using Xunit.Abstractions;

namespace Libplanet.Tests.Blockchain.Policies;

public class BlockPolicyTest : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly StoreFixture _fx;
    private readonly BlockChain _chain;
    private readonly BlockChainOptions _policy;

    public BlockPolicyTest(ITestOutputHelper output)
    {
        _fx = new MemoryStoreFixture();
        _output = output;
        _policy = new BlockChainOptions
        {
            BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
        };
        var repository = new Repository();
        _chain = new BlockChain(_fx.GenesisBlock, repository, _policy);
    }

    public void Dispose()
    {
        _fx.Dispose();
    }

    [Fact]
    public void Constructors()
    {
        var tenSec = new TimeSpan(0, 0, 10);
        var a = new BlockChainOptions
        {
            BlockInterval = tenSec,
        };
        Assert.Equal(tenSec, a.BlockInterval);

        var b = new BlockChainOptions
        {
            BlockInterval = TimeSpan.FromMilliseconds(65000),
        };
        Assert.Equal(
            new TimeSpan(0, 1, 5),
            b.BlockInterval);

        var c = new BlockChainOptions();
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

        var options = new BlockChainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };

        // Valid Transaction

        var validTx = new TransactionBuilder
        {
            Blockchain = _chain,
            Signer = validKey,
        }.Create();
        _chain.StagedTransactions.Add(validTx);
        options.TransactionOptions.Validate(validTx);

        // Invalid Transaction
        var invalidKey = new PrivateKey();
        var invalidTx = new TransactionBuilder
        {
            Blockchain = _chain,
            Signer = invalidKey,
        }.Create();
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
        var options = new BlockChainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };

        var invalidTx = new TransactionBuilder
        {
            Blockchain = _chain,
            Signer = invalidKey,
        }.Create();
        _chain.StagedTransactions.Add(invalidTx);
        options.TransactionOptions.Validate(invalidTx);

        // Invalid Transaction with Inner-exception.
        options = new BlockChainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValidWithInnerException),
            },
        };

        invalidTx = new TransactionBuilder
        {
            Blockchain = _chain,
            Signer = invalidKey,
        }.Create();
        _chain.StagedTransactions.Add(invalidTx);
    }

    [Fact]
    public void GetMinTransactionsPerBlock()
    {
        const int policyLimit = 2;

        var options = new BlockChainOptions
        {
            PolicyActions = new PolicyActions
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
            Blockchain = chain,
            Signer = privateKey,
        }.Create();
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

        var store = new Libplanet.Store.Repository(new MemoryDatabase());
        var stateStore = new TrieStateStore();
        var policy = new BlockChainOptions
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
                        Blockchain = chain,
                        Signer = privateKey,
                    }.Create();
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

        var options = new BlockChainOptions
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
                        Blockchain = chain,
                        Signer = key,
                    }.Create();
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
