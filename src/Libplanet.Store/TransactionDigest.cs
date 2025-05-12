using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

[Model(Version = 1)]
public sealed record class TransactionDigest : IEquatable<TransactionDigest>
{
    [Property(0)]
    public required Transaction Transaction { get; init; }

    [Property(1)]
    public required BlockHash BlockHash { get; init; }

}
