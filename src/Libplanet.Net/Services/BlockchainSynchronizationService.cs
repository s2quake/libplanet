using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class BlockchainSynchronizationService(
    Blockchain blockchain, ITransport transport, PeerExplorer peerExplorer)
    : ServiceBase
{
    private readonly BlockFetcher blockFetcher = new(blockchain, transport);

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
