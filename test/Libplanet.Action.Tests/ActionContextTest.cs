using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;

namespace Libplanet.Action.Tests
{
    public class ActionContextTest
    {
        private readonly System.Random _random;
        private readonly Address _address;
        private readonly TxId _txid;
        private readonly BlockCommit _lastCommit;

        public ActionContextTest()
        {
            _random = new System.Random();
            _address = _random.NextAddress();
            _txid = _random.NextTxId();
            var key = new PrivateKey();
            var hash = _random.NextBlockHash();
            _lastCommit = new BlockCommit
            {
                Height = 0,
                Round = 0,
                BlockHash = hash,
                Votes =
                [
                    new VoteMetadata
                    {
                        Height = 0,
                        Round = 0,
                        BlockHash = hash,
                        Timestamp = DateTimeOffset.UtcNow,
                        ValidatorPublicKey = key.PublicKey,
                        ValidatorPower = BigInteger.One,
                        Flag = VoteFlag.PreCommit,
                    }.Sign(key),
                ],
            };
        }

        [Fact]
        public void RandomShouldBeDeterministic()
        {
            (int Seed, int Expected)[] testCases =
            {
                (0, 1559595546),
                (1, 534011718),
            };
            foreach (var (seed, expected) in testCases)
            {
                var context = new ActionContext
                {
                    Signer = _address,
                    TxId = _txid,
                    Proposer = _address,
                    BlockHeight = 1,
                    BlockProtocolVersion = Block.CurrentProtocolVersion,
                    LastCommit = _lastCommit,
                    RandomSeed = seed,
                };
                IRandom random = context.GetRandom();
                Assert.Equal(expected, random.Next());
            }
        }

        [Fact]
        public void GuidShouldBeDeterministic()
        {
            var context1 = new ActionContext
            {
                Signer = _address,
                TxId = _txid,
                Proposer = _address,
                BlockHeight = 1,
                BlockProtocolVersion = Block.CurrentProtocolVersion,
                LastCommit = _lastCommit,
                RandomSeed = 0,
            };

            var context2 = new ActionContext
            {
                Signer = _address,
                TxId = _txid,
                Proposer = _address,
                BlockHeight = 1,
                BlockProtocolVersion = Block.CurrentProtocolVersion,
                LastCommit = _lastCommit,
            };

            var context3 = new ActionContext
            {
                Signer = _address,
                TxId = _txid,
                Proposer = _address,
                BlockHeight = 1,
                BlockProtocolVersion = Block.CurrentProtocolVersion,
                LastCommit = _lastCommit,
            };

            (Guid Expected, Guid Diff)[] testCases =
            {
                (
                    new Guid("6f460c1a-755d-48e4-ad67-65d5f519dbc8"),
                    new Guid("8286d046-9740-43e4-95cf-ff46699c73c4")
                ),
                (
                    new Guid("3b347c2b-f837-4085-ac5e-64005393b30d"),
                    new Guid("3410cda1-5b13-434e-af84-a54adf7a0ea0")
                ),
            };

            var rand1 = context1.GetRandom();
            var rand2 = context2.GetRandom();
            var rand3 = context3.GetRandom();
            foreach (var (expected, diff) in testCases)
            {
                Assert.Equal(expected, rand1.NextGuid());
                Assert.Equal(expected, rand2.NextGuid());
                Assert.Equal(diff, rand3.NextGuid());
            }
        }

        [Fact]
        public void GuidVersionAndVariant()
        {
            for (var i = 0; i < 100; i++)
            {
                var context = new ActionContext
                {
                    Signer = _address,
                    TxId = _txid,
                    Proposer = _address,
                    BlockHeight = 1,
                    BlockProtocolVersion = Block.CurrentProtocolVersion,
                    LastCommit = _lastCommit,
                    RandomSeed = i,
                };
                var guid = context.GetRandom().NextGuid().ToString();

                Assert.Equal('4', guid[14]);
                Assert.True(guid[19] >= '8' && guid[19] <= 'b');
            }
        }
    }
}
