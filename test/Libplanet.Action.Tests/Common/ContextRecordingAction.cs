using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

[Model(Version = 1)]
public sealed record class ContextRecordingAction : ActionBase
{
    public static readonly Address MinerRecordAddress =
        Address.Parse("1000000000000000000000000000000000000001");

    public static readonly Address SignerRecordAddress =
        Address.Parse("1000000000000000000000000000000000000002");

    public static readonly Address BlockIndexRecordAddress =
        Address.Parse("1000000000000000000000000000000000000003");

    public static readonly Address RandomRecordAddress =
        Address.Parse("1000000000000000000000000000000000000004");

    [Property(0)]
    public Address Address { get; init; }

    [Property(1)]
    public required IValue Value { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        world[ReservedAddresses.LegacyAccount, Address] = Value;
        world[ReservedAddresses.LegacyAccount, Address] = Value;
        world[ReservedAddresses.LegacyAccount, MinerRecordAddress] = new Binary(context.Miner.Bytes);
        world[ReservedAddresses.LegacyAccount, SignerRecordAddress] = new Binary(context.Signer.Bytes);
        world[ReservedAddresses.LegacyAccount, BlockIndexRecordAddress] = new Integer(context.BlockHeight);
        world[ReservedAddresses.LegacyAccount, RandomRecordAddress] = new Integer(context.GetRandom().Next());
    }
}
