namespace Libplanet.Net;

public sealed record class PeerBlockchainState(
    Peer Peer,
    BlockSummary Genesis,
    BlockSummary Tip);
