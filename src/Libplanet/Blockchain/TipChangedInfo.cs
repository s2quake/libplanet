using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed record class TipChangedInfo(
    Block OldTip,
    Block NewTip);
