using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Types;

namespace Libplanet.Net.Tests;

public static class TestUtils
{
    public const int Timeout = 30000;

    public static readonly TimeSpan WaitTimeout1 = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan WaitTimeout2 = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan WaitTimeout3 = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan WaitTimeout5 = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan WaitTimeout10 = TimeSpan.FromSeconds(10);

    public static readonly ImmutableArray<ISigner> Signers = Libplanet.Tests.TestUtils.Signers;

    public static readonly ImmutableSortedSet<Validator> Validators = Libplanet.Tests.TestUtils.Validators;

    public static readonly BlockchainOptions BlockchainOptions = new()
    {
        SystemAction = new SystemAction
        {
            LeaveBlockActions = [new MinerReward(1)],
        },
        BlockOptions = new BlockOptions
        {
            MaxActionBytes = 50 * 1024,
        },
    };

    public static ISigner GeneratePrivateKeyOfBucketIndex(Address tableAddress, int target)
    {
        var table = new PeerCollection(tableAddress);
        var targetBucket = table.Buckets[target];
        PrivateKey privateKey;
        do
        {
            privateKey = new PrivateKey();
        }
        while (table.Buckets[privateKey.Address] != targetBucket);

        return privateKey.AsSigner();
    }

    public static BlockCommit CreateBlockCommit(Block block)
        => Libplanet.Tests.TestUtils.CreateBlockCommit(block);

    public static BlockCommit CreateBlockCommit(BlockHash blockHash, int height, int round)
        => Libplanet.Tests.TestUtils.CreateBlockCommit(blockHash, height, round);

    public static ITransport CreateTransport(
        ISigner? signer = null,
        TransportOptions? options = null)
    {
        options ??= new TransportOptions();
        signer ??= new PrivateKey().AsSigner();
        return new Libplanet.Net.NetMQ.NetMQTransport(signer, options);
    }

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
