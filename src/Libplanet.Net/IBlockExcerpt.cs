using Libplanet.Types.Blocks;

namespace Libplanet.Net;

public interface IBlockExcerpt
{
    int ProtocolVersion { get; }

    long Index { get; }

    BlockHash Hash { get; }
}
