using Libplanet.Serialization;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Common;

[Model(Version = 1)]
public sealed record class MinerReward : ActionBase
{
    public static readonly Address RewardRecordAddress = Address.Parse("0000000000000000000000000000000000000000");

    public MinerReward()
    {
    }

    public MinerReward(int reward)
    {
        Reward = reward;
    }

    [Property(0)]
    public int Reward { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        var proposer = context.Proposer;
        var record = world.GetValueOrDefault(SystemAccount, RewardRecordAddress, string.Empty);
        var reward = world.GetValueOrDefault(SystemAccount, proposer, 0) + Reward;
        var rewardRecord = record == string.Empty ? $"{proposer}" : $"{record},{proposer}";
        world[SystemAccount, RewardRecordAddress] = rewardRecord;
        world[SystemAccount, proposer] = reward;
    }
}
