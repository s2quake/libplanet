using System.Net;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;

namespace Libplanet.Net.Tests;

public static class TestUtils
{
    public const int Timeout = 30000;

    public static readonly TimeSpan WaitTimeout1 = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan WaitTimeout2 = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan WaitTimeout5 = TimeSpan.FromSeconds(5);

    public static readonly BlockHash BlockHash0 =
        BlockHash.Parse(
            "042b81bef7d4bca6e01f5975ce9ac7ed9f75248903d08836bed6566488c8089d");

    public static readonly ImmutableList<PrivateKey> PrivateKeys =
        Libplanet.Tests.TestUtils.ValidatorPrivateKeys;

    public static readonly ImmutableArray<ISigner> Signers = [.. PrivateKeys.Select(item => item.AsSigner())];

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

    [Obsolete]
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

    public static ITransport CreateTransport(
        PrivateKey privateKey,
        TransportOptions? options = null)
    {
        options ??= new TransportOptions();
        return new Libplanet.Net.NetMQ.NetMQTransport(privateKey.AsSigner(), options);
    }

    public static ITransport CreateTransport(
        ISigner? signer = null,
        TransportOptions? options = null)
    {
        options ??= new TransportOptions();
        signer ??= new PrivateKey().AsSigner();
        return new Libplanet.Net.NetMQ.NetMQTransport(signer, options);
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
}
