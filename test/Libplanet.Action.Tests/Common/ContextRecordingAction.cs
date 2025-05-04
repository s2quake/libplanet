using Bencodex.Types;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;
using static Libplanet.Action.State.ReservedAddresses;

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

    [Property(1, KnownTypes = new[] { typeof(int), typeof(string) })]
    public required object Value { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        world[LegacyAccount, Address] = Value;
        world[LegacyAccount, Address] = Value;
        world[LegacyAccount, MinerRecordAddress] = context.Proposer;
        world[LegacyAccount, SignerRecordAddress] = context.Signer;
        world[LegacyAccount, BlockIndexRecordAddress] = context.BlockHeight;
        world[LegacyAccount, RandomRecordAddress] = context.GetRandom().Next();
    }
}
