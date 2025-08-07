using System.Net;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Random = System.Random;
using Libplanet.Net.Tests.Consensus;
using Libplanet.Net.NetMQ;

namespace Libplanet.Net.Tests;

public static class TestUtils
{
    public static readonly BlockHash BlockHash0 =
        BlockHash.Parse(
            "042b81bef7d4bca6e01f5975ce9ac7ed9f75248903d08836bed6566488c8089d");

    public static readonly ImmutableList<PrivateKey> PrivateKeys =
        Libplanet.Tests.TestUtils.ValidatorPrivateKeys;

    public static readonly ImmutableHashSet<Peer> Peers =
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
        var table = new PeerCollection(tableAddress);
        var targetBucket = table.Buckets[target];
        PrivateKey privateKey;
        do
        {
            privateKey = new PrivateKey();
        }
        while (table.Buckets[privateKey.Address] != targetBucket);

        return privateKey;
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
        ConsensusService consensusContext,
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
                        Round = context.Round.Index,
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
                        Round = context.Round.Index,
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
        ConsensusService consensusContext,
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

    public static ITransport CreateTransport(
        PrivateKey? privateKey = null,
        int? port = null,
        TransportOptions? options = null)
    {
        options ??= new TransportOptions
        {
            Host = "127.0.0.1",
            Port = port ?? 0,
        };

        privateKey ??= new PrivateKey();

        return new Libplanet.Net.NetMQ.NetMQTransport(privateKey.AsSigner(), options);
    }


    public static Net.Consensus.Consensus CreateConsensus(
        Blockchain? blockchain = null,
        int height = 1,
        PrivateKey? privateKey = null,
        ImmutableSortedSet<Validator>? validators = null,
        ConsensusOptions? options = null)
    {
        blockchain ??= Libplanet.Tests.TestUtils.MakeBlockchain();
        var consensus = new Net.Consensus.Consensus(
            blockchain,
            height,
            (privateKey ?? PrivateKeys[1]).AsSigner(),
            validators: validators ?? Validators,
            options: options ?? new ConsensusOptions());

        consensus.ShouldPropose.Subscribe(consensus.Propose);
        consensus.ShouldPreVote.Subscribe(consensus.PreVote);
        consensus.ShouldPreCommit.Subscribe(consensus.PreCommit);
        consensus.Completed.Subscribe(e =>
        {
            _ = Task.Run(() => blockchain.Append(e.Block, e.BlockCommit));
        });

        return consensus;
    }

    public static ConsensusService CreateConsensusService(
        ITransport transport,
        Blockchain? blockchain = null,
        PrivateKey? key = null,
        ImmutableHashSet<Peer>? validatorPeers = null,
        TimeSpan? newHeightDelay = null,
        ConsensusOptions? consensusOption = null)
    {
        blockchain ??= Libplanet.Tests.TestUtils.MakeBlockchain();
        key ??= PrivateKeys[1];
        validatorPeers ??= Peers; ;

        var signer = key.AsSigner();
        var consensusServiceOptions = new ConsensusServiceOptions
        {
            Validators = [.. validatorPeers.Where(peer => peer.Address != key.Address)],
            TargetBlockInterval = newHeightDelay ?? TimeSpan.FromMilliseconds(10_000),
            ConsensusOptions = consensusOption ?? new ConsensusOptions(),
        };

        return new ConsensusService(signer, blockchain, transport, consensusServiceOptions);
    }

    public static Blockchain MakeBlockchain(
        BlockchainOptions? options = null,
        IEnumerable<IAction>? actions = null,
        ImmutableSortedSet<Validator>? validatorSet = null,
        PrivateKey? privateKey = null,
        DateTimeOffset? timestamp = null,
        Block? genesisBlock = null,
        int protocolVersion = BlockHeader.CurrentProtocolVersion)
        => Libplanet.Tests.TestUtils.MakeBlockchain(
            options: options ?? BlockchainOptions,
            actions: actions,
            validatorSet: validatorSet ?? Validators,
            privateKey: privateKey ?? PrivateKeys[0],
            timestamp: timestamp,
            genesisBlock: genesisBlock,
            protocolVersion: protocolVersion);

    public static void InvokeDelay(Action action, int millisecondsDelay)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(millisecondsDelay);
            action();
        });
    }

    public static void InvokeDelay(Func<Task> func, int millisecondsDelay)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(millisecondsDelay);
            await func();
        });
    }

    // public static Task<T> InvokeDelay<T>(Func<T> func, int millisecondsDelay)
    //     => InvokeDelay(func, TimeSpan.FromMilliseconds(millisecondsDelay));

    // public static async Task<T> InvokeDelay<T>(Func<T> func, TimeSpan delay)
    // {
    //     await Task.Delay(delay);
    //     return func();
    // }

    // public static Task<T> InvokeDelay<T>(Func<Task<T>> func, int millisecondsDelay)
    //     => InvokeDelay(func, TimeSpan.FromMilliseconds(millisecondsDelay));

    // public static async Task<T> InvokeDelay<T>(Func<Task<T>> func, TimeSpan delay)
    // {
    //     await Task.Delay(delay);
    //     return await func();
    // }
}
