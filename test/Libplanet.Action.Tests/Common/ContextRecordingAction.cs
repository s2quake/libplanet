using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using static Libplanet.Action.SystemAddresses;

namespace Libplanet.Action.Tests.Common;

[Model(Version = 1)]
public sealed record class ContextRecordingAction : ActionBase
{
    public static readonly Address MinerRecordAddress = Address.Parse("1000000000000000000000000000000000000001");
    public static readonly Address SignerRecordAddress = Address.Parse("1000000000000000000000000000000000000002");
    public static readonly Address BlockIndexRecordAddress = Address.Parse("1000000000000000000000000000000000000003");
    public static readonly Address RandomRecordAddress = Address.Parse("1000000000000000000000000000000000000004");

    [Property(0)]
    public Address Address { get; init; }

    [Property(1)]
    public required object Value { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        world[SystemAccount, Address] = Value;
        world[SystemAccount, Address] = Value;
        world[SystemAccount, MinerRecordAddress] = context.Proposer;
        world[SystemAccount, SignerRecordAddress] = context.Signer;
        world[SystemAccount, BlockIndexRecordAddress] = context.BlockHeight;
        world[SystemAccount, RandomRecordAddress] = context.GetRandom().Next();
    }
}
