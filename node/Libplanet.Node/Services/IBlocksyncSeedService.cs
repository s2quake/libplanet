namespace Libplanet.Node.Services;

public interface IBlocksyncSeedService
{
    Net.Peer BoundPeer { get; }
}
