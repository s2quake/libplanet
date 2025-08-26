using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Data;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Libplanet.TestUtilities;

namespace Libplanet.Tests.Blockchain;

public class BlockchainOptionsTest(ITestOutputHelper output)
{
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

        var validTx = blockchain.CreateTransaction(validSigner);
        blockchain.StagedTransactions.Add(validTx);
        options.TransactionOptions.Validate(validTx);

        // Invalid Transaction
        var invalidTx = blockchain.CreateTransaction(invalidSigner);
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

        var invalidTx = blockchainA.CreateTransaction(invalidSigner);
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

        invalidTx = blockchainA.CreateTransaction(invalidSigner);
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
                MinTransactions = policyLimit,
            },
        };
        var signer = new PrivateKey().AsSigner();
        var chain = TestUtils.MakeBlockchain(options);

        var tx = chain.CreateTransaction(signer);
        chain.StagedTransactions.Add(tx);
        Assert.Single(chain.StagedTransactions.Collect());

        // Tests if MineBlock() method will throw an exception if less than the minimum
        // transactions are present
        Assert.Throws<OperationCanceledException>(
            () => chain.Propose(RandomUtility.Signer()));
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
                MaxTransactions = policyLimit,
            },
        };
        var signer = new PrivateKey().AsSigner();
        var chain = TestUtils.MakeBlockchain(policy);

        _ = Enumerable
                .Range(0, generatedTxCount)
                .Select(_ =>
                {
                    var tx = chain.CreateTransaction(signer);
                    chain.StagedTransactions.Add(tx);
                    return tx;
                })
                .ToList();
        Assert.Equal(generatedTxCount, chain.StagedTransactions.Collect().Length);

        var block = chain.Propose(signer);
        Assert.Equal(policyLimit, block.Transactions.Count);
    }

    [Fact]
    public void GetMaxTransactionsPerSignerPerBlock()
    {
        const int keyCount = 2;
        const int generatedTxCount = 10;
        const int policyLimit = 4;

        var random = RandomUtility.GetRandom(output);
        var options = new BlockchainOptions
        {
            BlockOptions = new BlockOptions
            {
                MaxTransactionsPerSigner = policyLimit,
            },
        };
        var signers = RandomUtility.Array(random, RandomUtility.Signer, keyCount);
        var minerSigner = signers[0];
        var blockchain = TestUtils.MakeBlockchain(options);

        foreach (var signer in signers)
        {
            for (var i = 0; i < generatedTxCount; i++)
            {
                var tx = blockchain.CreateTransaction(signer);
                blockchain.StagedTransactions.Add(tx);
            }
        }

        Assert.Equal(generatedTxCount * keyCount, blockchain.StagedTransactions.Collect().Length);

        var block = blockchain.Propose(minerSigner);
        Assert.Equal(policyLimit * keyCount, block.Transactions.Count);

        foreach (var signer in signers)
        {
            Assert.Equal(
                policyLimit,
                block.Transactions.Count(tx => tx.Signer.Equals(signer.Address)));
        }
    }
}
