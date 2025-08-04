namespace Libplanet.Node.Services;

public interface IConsensusSeedService
{
    Net.Peer BoundPeer { get; }
}
