using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using Serilog;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Common;

[Model(Version = 1)]
public sealed record class DelayAction : ActionBase
{
    public static readonly Address TrivialUpdatedAddress = Address.Parse("3d94abf05556fdae0755ff4427869f80afd06b58");

    public DelayAction()
    {
    }

    public DelayAction(int milliSecond)
    {
        MilliSecond = milliSecond;
    }

    [Property(0)]
    public int MilliSecond { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var started = DateTimeOffset.UtcNow;
        Log.Debug(
            "{MethodName} exec started. Delay target: {MilliSecond}",
            nameof(DelayAction),
            MilliSecond);
        Thread.Sleep(MilliSecond);
        var ended = DateTimeOffset.UtcNow;
        world[SystemAccount, TrivialUpdatedAddress] = MilliSecond;
        Log.Debug(
            "{MethodName} Total Executed Time: {Elapsed}. Delay target: {MilliSecond}",
            nameof(DelayAction),
            ended - started,
            MilliSecond);
    }
}
