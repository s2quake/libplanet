using Libplanet.Action.State;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

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
        var record = world.GetValue(ReservedAddresses.LegacyAccount, RewardRecordAddress, string.Empty);
        var reward = world.GetValue(ReservedAddresses.LegacyAccount, proposer, 0) + Reward;
        var rewardRecord = record == string.Empty ? $"{proposer}" : $"{record},{proposer}";
        world[ReservedAddresses.LegacyAccount, RewardRecordAddress] = rewardRecord;
        world[ReservedAddresses.LegacyAccount, proposer] = reward;
    }
}
