using Libplanet.Types.Blocks;

namespace Libplanet;

public sealed record class TipChangedInfo(
    Block OldTip,
    Block NewTip);
