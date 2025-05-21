using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed record class TipChangedEvent(
    Block OldTip,
    Block NewTip);
