using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "BlockSummary")]
public sealed partial record class BlockSummary
{
    [Property(0)]
    public int BlockVersion { get; init; }

    [Property(1)]
    public int Height { get; init; }

    [Property(2)]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; }

    public static implicit operator BlockSummary(Block block) => new()
    {
        BlockVersion = block.Header.BlockVersion,
        Height = block.Header.Height,
        BlockHash = block.BlockHash,
        Timestamp = block.Header.Timestamp,
    };
}
