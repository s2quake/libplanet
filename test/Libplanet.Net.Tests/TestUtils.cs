using System.Net;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net;
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
using Libplanet.Net.Tests.Consensus;

namespace Libplanet.Net.Tests;

public static class TestUtils
{
    public static readonly BlockHash BlockHash0 =
        BlockHash.Parse(
            "042b81bef7d4bca6e01f5975ce9ac7ed9f75248903d08836bed6566488c8089d");

    public static readonly ImmutableList<PrivateKey> PrivateKeys =
        Libplanet.Tests.TestUtils.ValidatorPrivateKeys;

    public static readonly ImmutableArray<Peer> Peers =
    [
        new Peer { Address = PrivateKeys[0].Address, EndPoint = new DnsEndPoint("1.0.0.0", 1000)},
        new Peer { Address = PrivateKeys[1].Address, EndPoint = new DnsEndPoint("1.0.0.1", 1001)},
        new Peer { Address = PrivateKeys[2].Address, EndPoint = new DnsEndPoint("1.0.0.2", 1002)},
        new Peer { Address = PrivateKeys[3].Address, EndPoint = new DnsEndPoint("1.0.0.3", 1003)},
    ];

    public static readonly ImmutableSortedSet<Validator> Validators = Libplanet.Tests.TestUtils.Validators;

    public static readonly BlockchainOptions BlockchainOptions = new()
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

    public static readonly Protocol Protocol = new ProtocolMetadata
    {
        Version = 1,
        Signer = PrivateKeys[0].Address,
    }.Sign(PrivateKeys[0]);

    private static readonly Random Random = new();

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

    public static Blockchain CreateBlockchain(BlockchainOptions? options = null, Block? genesisBlock = null)
    {
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockChain(
            options: options ?? BlockchainOptions,
            genesisBlock: genesisBlock);
        return blockchain;
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
                BlockHash = block.BlockHash,
                Height = height,
                Round = round,
                Timestamp = DateTimeOffset.UtcNow,
                Proposer = privateKey.Address,
                ValidRound = validRound,
            }.Sign(privateKey, block)
        };
    }

    public static BlockCommit CreateBlockCommit(Block block) =>
        Libplanet.Tests.TestUtils.CreateBlockCommit(block);

    public static BlockCommit CreateBlockCommit(BlockHash blockHash, int height, int round) =>
        Libplanet.Tests.TestUtils.CreateBlockCommit(blockHash, height, round);

    public static async Task HandleFourPeersPreCommitMessages(
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

            await consensusContext.HandleMessageAsync(
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
                },
                default);
        }
    }

    public static void HandleFourPeersPreCommitMessages(
        Net.Consensus.Consensus context,
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
        Net.Consensus.Consensus context,
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

    public static async Task HandleFourPeersPreVoteMessages(
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

            await consensusContext.HandleMessageAsync(
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
                },
                default);
        }
    }

    public static NetMQTransport CreateTransport(
        PrivateKey? privateKey = null,
        int? port = null,
        TransportOptions? options = null)
    {
        options ??= new TransportOptions
        {
            Protocol = TestUtils.Protocol,
            Host = "127.0.0.1",
            Port = port ?? 0,
        };

        privateKey ??= new PrivateKey();

        return new NetMQTransport(privateKey.AsSigner(), options);
    }


    public static Net.Consensus.Consensus CreateConsensus(
        Blockchain? blockchain = null,
        int height = 1,
        PrivateKey? privateKey = null,
        ImmutableSortedSet<Validator>? validators = null,
        ConsensusOptions? options = null)
    {
        blockchain ??= CreateBlockchain();
        var consensus = new Net.Consensus.Consensus(
            blockchain,
            height,
            (privateKey ?? PrivateKeys[1]).AsSigner(),
            validators: validators ?? Validators,
            options: options ?? new ConsensusOptions());

        consensus.BlockPropose.Subscribe(consensus.Post);
        consensus.PreVote.Subscribe(consensus.Post);
        consensus.PreCommit.Subscribe(consensus.Post);
        consensus.Completed.Subscribe(e =>
        {
            _ = Task.Run(() => blockchain.Append(e.Block, e.BlockCommit));
        });

        return consensus;
    }

    public static ConsensusReactor CreateConsensusReactor(
        Blockchain? blockchain = null,
        PrivateKey? key = null,
        string host = "127.0.0.1",
        int port = 0,
        ImmutableArray<Peer>? validatorPeers = null,
        TimeSpan? newHeightDelay = null,
        ConsensusOptions? consensusOption = null)
    {
        blockchain ??= CreateBlockchain();
        key ??= PrivateKeys[1];

        var signer = key.AsSigner();
        var consensusReactorOptions = new ConsensusReactorOptions
        {
            Validators = validatorPeers ?? Peers,
            TargetBlockInterval = newHeightDelay ?? TimeSpan.FromMilliseconds(10_000),
            ConsensusOptions = consensusOption ?? new ConsensusOptions(),
            TransportOptions = new TransportOptions
            {
                Protocol = Protocol,
                Host = host,
                Port = port,
            }
        };

        return new ConsensusReactor(signer, blockchain, consensusReactorOptions);
    }

    public static byte[] GetRandomBytes(int size)
    {
        var bytes = new byte[size];
        Random.NextBytes(bytes);

        return bytes;
    }
}
