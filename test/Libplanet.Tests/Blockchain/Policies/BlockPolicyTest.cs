using Libplanet.Action;
using Libplanet.Action.Tests.Common;
using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Tests.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using Xunit.Abstractions;

namespace Libplanet.Tests.Blockchain.Policies
{
    public class BlockPolicyTest : IDisposable
    {

        private readonly ITestOutputHelper _output;

        private StoreFixture _fx;
        private BlockChain _chain;
        private BlockChainOptions _policy;

        public BlockPolicyTest(ITestOutputHelper output)
        {
            _fx = new MemoryStoreFixture();
            _output = output;
            _policy = new BlockChainOptions
            {
                BlockInterval = TimeSpan.FromMilliseconds(3 * 60 * 60 * 1000),
            };
            _chain = BlockChain.Create(
                _policy,
                _fx.Store,
                _fx.StateStore,
                _fx.GenesisBlock);
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

            void IsSignerValid(BlockChain chain, Transaction tx)
            {
                var validAddress = validKey.Address;
                if (!tx.Signer.Equals(validAddress))
                {
                    throw new InvalidOperationException("invalid signer");
                }
            }

            var policy = new BlockChainOptions
            {
                TransactionValidation = IsSignerValid,
            };

            // Valid Transaction
            var validTx = _chain.MakeTransaction(validKey, new DumbAction[] { });
            policy.ValidateTransaction(_chain, validTx);

            // Invalid Transaction
            var invalidKey = new PrivateKey();
            var invalidTx = _chain.MakeTransaction(invalidKey, new DumbAction[] { });
            policy.ValidateTransaction(_chain, invalidTx);
        }

        [Fact]
        public void ValidateNextBlockTxWithInnerException()
        {
            var validKey = new PrivateKey();
            var invalidKey = new PrivateKey();

            void IsSignerValid(BlockChain chain, Transaction tx)
            {
                var validAddress = validKey.Address;
                if (!tx.Signer.Equals(validAddress))
                {
                    throw new InvalidOperationException("invalid signer");
                }
            }

            //Invalid Transaction with inner-exception
            void IsSignerValidWithInnerException(BlockChain chain, Transaction tx)
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
            var policy = new BlockChainOptions
            {
                TransactionValidation = IsSignerValid,
            };

            var invalidTx = _chain.MakeTransaction(invalidKey, new DumbAction[] { });
            policy.ValidateTransaction(_chain, invalidTx);

            // Invalid Transaction with Inner-exception.
            policy = new BlockChainOptions
            {
                TransactionValidation = IsSignerValidWithInnerException,
            };

            invalidTx = _chain.MakeTransaction(invalidKey, new DumbAction[] { });
        }

        [Fact]
        public void GetMinTransactionsPerBlock()
        {
            const int policyLimit = 2;

            var store = new MemoryStore();
            var stateStore = new TrieStateStore();
            var policy = new BlockChainOptions
            {
                PolicyActions = new PolicyActions
                {
                    EndBlockActions = [new MinerReward(1)],
                },
                MinTransactionsPerBlock = policyLimit,
            };
            var privateKey = new PrivateKey();
            var chain = TestUtils.MakeBlockChain(policy, store, stateStore);

            _ = chain.MakeTransaction(privateKey, new DumbAction[] { });
            Assert.Single(chain.ListStagedTransactions());

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

            var store = new MemoryStore();
            var stateStore = new TrieStateStore();
            var policy = new BlockChainOptions
            {
                MaxTransactionsPerBlock = policyLimit,
            };
            var privateKey = new PrivateKey();
            var chain = TestUtils.MakeBlockChain(policy, store, stateStore);

            _ = Enumerable
                    .Range(0, generatedTxCount)
                    .Select(_ => chain.MakeTransaction(privateKey, new DumbAction[] { }))
                    .ToList();
            Assert.Equal(generatedTxCount, chain.ListStagedTransactions().Count);

            var block = chain.ProposeBlock(privateKey);
            Assert.Equal(policyLimit, block.Transactions.Count);
        }

        [Fact]
        public void GetMaxTransactionsPerSignerPerBlock()
        {
            const int keyCount = 2;
            const int generatedTxCount = 10;
            const int policyLimit = 4;

            var store = new MemoryStore();
            var stateStore = new TrieStateStore();
            var policy = new BlockChainOptions
            {
                MaxTransactionsPerSignerPerBlock = policyLimit,
            };
            var privateKeys = Enumerable.Range(0, keyCount).Select(_ => new PrivateKey()).ToList();
            var minerKey = privateKeys.First();
            var chain = TestUtils.MakeBlockChain(policy, store, stateStore);

            privateKeys.ForEach(
                key => _ = Enumerable
                    .Range(0, generatedTxCount)
                    .Select(_ => chain.MakeTransaction(key, new DumbAction[] { }))
                    .ToList());
            Assert.Equal(generatedTxCount * keyCount, chain.ListStagedTransactions().Count);

            var block = chain.ProposeBlock(minerKey);
            Assert.Equal(policyLimit * keyCount, block.Transactions.Count);
            privateKeys.ForEach(
                key => Assert.Equal(
                    policyLimit,
                    block.Transactions.Count(tx => tx.Signer.Equals(key.Address))));
        }
    }
}
