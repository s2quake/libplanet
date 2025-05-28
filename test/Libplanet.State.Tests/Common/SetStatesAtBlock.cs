using Libplanet.Serialization;
using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.State.Tests.Common;

[Model(Version = 1)]
public sealed record class SetStatesAtBlock : ActionBase
{
    public SetStatesAtBlock()
    {
    }

    public SetStatesAtBlock(Address address, object value, Address accountAddress, int blockHeight)
    {
        Address = address;
        BlockHeight = blockHeight;
        AccountAddress = accountAddress;
        Value = value;
    }

    [Property(0)]
    public Address Address { get; init; }

    [Property(1)]
    public object? Value { get; init; }

    [Property(2)]
    public Address AccountAddress { get; init; }

    [Property(3)]
    public int BlockHeight { get; init; }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        if (context.BlockHeight == BlockHeight && Value is not null)
        {
            world[AccountAddress, Address] = Value;
        }
    }
}
