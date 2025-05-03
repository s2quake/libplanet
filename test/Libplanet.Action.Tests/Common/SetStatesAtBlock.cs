using Bencodex.Types;
using Libplanet.Types.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Action.Tests.Common;

[Model(Version = 1)]
public sealed record class SetStatesAtBlock : ActionBase
{
    public SetStatesAtBlock()
    {
    }

    public SetStatesAtBlock(Address address, IValue value, Address accountAddress, long blockHeight)
    {
        Address = address;
        BlockHeight = blockHeight;
        AccountAddress = accountAddress;
        Value = value;
    }

    [Property(0)]
    public Address Address { get; init; }

    [Property(1)]
    public IValue Value { get; init; } = Null.Value;

    [Property(2)]
    public Address AccountAddress { get; init; }

    [Property(3)]
    public long BlockHeight { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        if (context.BlockHeight == BlockHeight)
        {
            world[AccountAddress, Address] = Value;
        }
    }
}
