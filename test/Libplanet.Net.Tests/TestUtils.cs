using System.Net;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Libplanet.Data;
using Random = System.Random;

namespace Libplanet.Net.Tests
{
    public static class TestUtils
    {
        public static readonly BlockHash BlockHash0 =
            BlockHash.Parse(
                "042b81bef7d4bca6e01f5975ce9ac7ed9f75248903d08836bed6566488c8089d");

        public static readonly ImmutableList<PrivateKey> PrivateKeys =
            Libplanet.Tests.TestUtils.ValidatorPrivateKeys;

        public static readonly List<Peer> Peers =
        [
            new Peer { Address = PrivateKeys[0].Address, EndPoint = new DnsEndPoint("1.0.0.0", 1000)},
            new Peer { Address = PrivateKeys[1].Address, EndPoint = new DnsEndPoint("1.0.0.1", 1001)},
            new Peer { Address = PrivateKeys[2].Address, EndPoint = new DnsEndPoint("1.0.0.2", 1002)},
            new Peer { Address = PrivateKeys[3].Address, EndPoint = new DnsEndPoint("1.0.0.3", 1003)},
        ];

        public static readonly ImmutableSortedSet<Validator> Validators
            = Libplanet.Tests.TestUtils.Validators;

        public static readonly BlockchainOptions Options = new()
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
            BlockOptions = new BlockOptions
            {
                MaxTransactionsBytes = 50 * 1024,
            },
        };

        public static Protocol Protocol = new ProtocolMetadata
        {
            Version = 1,
            Signer = PrivateKeys[0].Address,
        }.Sign(PrivateKeys[0]);

        private static readonly Random Random = new Random();

        public static Vote CreateVote(
            PrivateKey privateKey,
            BigInteger power,
            int height,
            int round,
            BlockHash hash,
            VoteType flag) =>
            new VoteMetadata
            {
                Height = height,
                Round = round,
                BlockHash = hash,
                Timestamp = DateTimeOffset.Now,
                Validator = privateKey.Address,
                ValidatorPower = power,
                Type = flag,
            }.Sign(privateKey);

        public static PrivateKey GeneratePrivateKeyOfBucketIndex(Address tableAddress, int target)
        {
            var table = new RoutingTable(tableAddress);
            PrivateKey privateKey;
            do
            {
                privateKey = new PrivateKey();
            }
            while (table.GetBucketIndexOf(privateKey.Address) != target);

            return privateKey;
        }

        public static Blockchain CreateDummyBlockChain(BlockchainOptions? options = null, Block? genesisBlock = null)
        {
            options ??= Options;
            var blockChain = Libplanet.Tests.TestUtils.MakeBlockChain(
                options, genesisBlock: genesisBlock);

            return blockChain;
        }

        public static ConsensusProposalMessage CreateConsensusPropose(
            Block block,
            PrivateKey privateKey,
            int height = 1,
            int round = 0,
            int validRound = -1)
        {
            return new ConsensusProposalMessage
            {
                Proposal = new ProposalMetadata
                {
                    Height = height,
                    Round = round,
                    Timestamp = DateTimeOffset.UtcNow,
                    Proposer = privateKey.Address,
                    // MarshaledBlock = ModelSerializer.SerializeToBytes(block),
                    ValidRound = validRound,
                }.Sign(privateKey, block)
            };
        }

        public static BlockCommit CreateBlockCommit(Block block) =>
            Libplanet.Tests.TestUtils.CreateBlockCommit(block);

        public static BlockCommit CreateBlockCommit(BlockHash blockHash, int height, int round) =>
            Libplanet.Tests.TestUtils.CreateBlockCommit(blockHash, height, round);

        public static void HandleFourPeersPreCommitMessages(
            ConsensusReactor consensusContext,
            PrivateKey nodePrivateKey,
            BlockHash roundBlockHash)
        {
            foreach ((PrivateKey privateKey, BigInteger power)
                     in PrivateKeys.Zip(
                         Validators.Select(v => v.Power),
                         (first, second) => (first, second)))
            {
                if (privateKey == nodePrivateKey)
                {
                    continue;
                }

                consensusContext.HandleMessage(
                    new ConsensusPreCommitMessage
                    {
                        PreCommit = new VoteMetadata
                        {
                            Height = consensusContext.Height,
                            Round = consensusContext.Round,
                            BlockHash = roundBlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = privateKey.Address,
                            ValidatorPower = power,
                            Type = VoteType.PreCommit,
                        }.Sign(privateKey)
                    });
            }
        }

        public static void HandleFourPeersPreCommitMessages(
            Context context,
            PrivateKey nodePrivateKey,
            BlockHash roundBlockHash)
        {
            foreach ((PrivateKey privateKey, BigInteger power)
                     in PrivateKeys.Zip(
                         Validators.Select(v => v.Power),
                         (first, second) => (first, second)))
            {
                if (privateKey == nodePrivateKey)
                {
                    continue;
                }

                context.ProduceMessage(
                    new ConsensusPreCommitMessage
                    {
                        PreCommit = new VoteMetadata
                        {
                            Height = context.Height,
                            Round = context.Round,
                            BlockHash = roundBlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = privateKey.Address,
                            ValidatorPower = power,
                            Type = VoteType.PreCommit,
                        }.Sign(privateKey)
                    });
            }
        }

        public static void HandleFourPeersPreVoteMessages(
            Context context,
            PrivateKey nodePrivateKey,
            BlockHash roundBlockHash)
        {
            foreach ((PrivateKey privateKey, BigInteger power)
                     in PrivateKeys.Zip(
                         Validators.Select(v => v.Power),
                         (first, second) => (first, second)))
            {
                if (privateKey == nodePrivateKey)
                {
                    continue;
                }

                context.ProduceMessage(
                    new ConsensusPreVoteMessage
                    {
                        PreVote = new VoteMetadata
                        {
                            Height = context.Height,
                            Round = context.Round,
                            BlockHash = roundBlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = privateKey.Address,
                            ValidatorPower = power,
                            Type = VoteType.PreVote,
                        }.Sign(privateKey)
                    });
            }
        }

        public static void HandleFourPeersPreVoteMessages(
            ConsensusReactor consensusContext,
            PrivateKey nodePrivateKey,
            BlockHash roundBlockHash)
        {
            foreach ((PrivateKey privateKey, BigInteger power)
                     in PrivateKeys.Zip(
                         Validators.Select(v => v.Power),
                         (first, second) => (first, second)))
            {
                if (privateKey == nodePrivateKey)
                {
                    continue;
                }

                consensusContext.HandleMessage(
                    new ConsensusPreVoteMessage
                    {
                        PreVote = new VoteMetadata
                        {
                            Height = consensusContext.Height,
                            Round = consensusContext.Round,
                            BlockHash = roundBlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = privateKey.Address,
                            ValidatorPower = power,
                            Type = VoteType.PreVote,
                        }.Sign(privateKey)
                    });
            }
        }

        public static (Blockchain BlockChain, ConsensusReactor ConsensusContext)
            CreateDummyConsensusContext(
                TimeSpan newHeightDelay,
                BlockchainOptions? policy = null,
                PrivateKey? privateKey = null,
                ContextOptions? contextOption = null)
        {
            policy ??= Options;
            var blockChain = CreateDummyBlockChain(policy);
            ConsensusReactor? consensusContext = null;

            privateKey ??= PrivateKeys[1];

            void BroadcastMessage(ConsensusMessage message) =>
                Task.Run(() =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    consensusContext!.HandleMessage(message);
                });

            // consensusContext = new ConsensusReactor(
            //     null,
            //     blockChain,
            //     privateKey,
            //     newHeightDelay,
            //     contextOption ?? new ContextOptions());

            return (blockChain, consensusContext);
        }

        public static Context CreateDummyContext(
            Blockchain blockChain,
            int height = 1,
            BlockCommit? previousCommit = null,
            PrivateKey? privateKey = null,
            ContextOptions? contextOption = null,
            ImmutableSortedSet<Validator>? validators = null)
        {
            Context? context = null;
            privateKey ??= PrivateKeys[0];
            context = new Context(
                blockChain,
                height,
                privateKey.AsSigner(),
                options: contextOption ?? new ContextOptions());
            using var _ = context.MessagePublished.Subscribe(message => context.ProduceMessage(message));
            return context;
        }

        public static (Blockchain BlockChain, Context Context)
            CreateDummyContext(
                int height = 1,
                BlockCommit? lastCommit = null,
                BlockchainOptions? policy = null,
                PrivateKey? privateKey = null,
                ContextOptions? contextOption = null,
                ImmutableSortedSet<Validator>? validatorSet = null)
        {
            Context? context = null;
            privateKey ??= PrivateKeys[1];
            policy ??= Options;

            var blockChain = CreateDummyBlockChain(policy);
            context = new Context(
                blockChain,
                height,
                privateKey.AsSigner(),
                options: contextOption ?? new ContextOptions());
            using var _ = context.MessagePublished.Subscribe(message => context.ProduceMessage(message));

            return (blockChain, context);
        }

        public static ConsensusReactor CreateDummyConsensusReactor(
            Blockchain blockChain,
            PrivateKey? key = null,
            string host = "127.0.0.1",
            int consensusPort = 5101,
            List<Peer>? validatorPeers = null,
            int newHeightDelayMilliseconds = 10_000,
            ContextOptions? contextOption = null)
        {
            key ??= PrivateKeys[1];
            validatorPeers ??= Peers;

            var transportOption = new TransportOptions
            {
                Protocol = Protocol,
                Host = host,
                Port = consensusPort,
            };
            var consensusTransport = new NetMQTransport(key.AsSigner(), transportOption);
            var consensusReactorOptions = new ConsensusReactorOptions
            {
                ConsensusPeers = validatorPeers.ToImmutableArray(),
                PrivateKey = key,
                TargetBlockInterval = TimeSpan.FromMilliseconds(newHeightDelayMilliseconds),
                ContextOptions = contextOption ?? new ContextOptions(),
            };

            return new ConsensusReactor(consensusTransport, blockChain, consensusReactorOptions);
        }

        public static byte[] GetRandomBytes(int size)
        {
            var bytes = new byte[size];
            Random.NextBytes(bytes);

            return bytes;
        }
    }
}
