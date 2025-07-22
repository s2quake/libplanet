namespace Libplanet.Net;

public sealed record class BlockchainState(
    Peer Peer,
    BlockSummary Genesis,
    BlockSummary Tip);
