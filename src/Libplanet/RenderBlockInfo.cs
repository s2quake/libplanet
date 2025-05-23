using Libplanet.Types.Blocks;

namespace Libplanet;

public readonly record struct RenderBlockInfo(
    Block OldTip,
    Block NewTip);
