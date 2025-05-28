using Libplanet.Types;

namespace Libplanet;

public readonly record struct RenderBlockInfo(
    Block OldTip,
    Block NewTip);
