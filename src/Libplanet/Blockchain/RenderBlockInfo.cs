using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public readonly record struct RenderBlockInfo(
    Block OldTip,
    Block NewTip);
