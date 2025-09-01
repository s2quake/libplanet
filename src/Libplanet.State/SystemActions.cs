using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.State;

[Model(Version = 1, TypeName = "SystemActions")]
public sealed partial record class SystemActions
{
    public static SystemActions Empty { get; } = new SystemActions();

    [Property(0)]
    public ImmutableArray<IAction> EnterBlockActions { get; init; } = [];

    [Property(1)]
    public ImmutableArray<IAction> LeaveBlockActions { get; init; } = [];

    [Property(2)]
    public ImmutableArray<IAction> EnterTxActions { get; init; } = [];

    [Property(3)]
    public ImmutableArray<IAction> LeaveTxActions { get; init; } = [];

    public HashDigest<SHA256> Hash => HashDigest<SHA256>.HashData(ModelSerializer.SerializeToBytes(this));
}
