using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "BlockExcerpt")]
public sealed partial record class BlockExcerpt
{
    [Property(0)]
    public int ProtocolVersion { get; init; }

    [Property(1)]
    public int Height { get; init; }

    [Property(2)]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; }

    public static implicit operator BlockExcerpt(Block block) => new()
    {
        ProtocolVersion = block.Header.Version,
        Height = block.Header.Height,
        BlockHash = block.BlockHash,
        Timestamp = block.Header.Timestamp,
    };
}
