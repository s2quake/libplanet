using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;

namespace Libplanet.Net.Services;

internal sealed class BlockDemanService(Blockchain blockchain, ITransport transport)
    : BackgroundServiceBase, IDisposable
{
    private readonly BlockDemandCollector _collector = new(blockchain, transport);

    protected override TimeSpan Interval { get; } = TimeSpan.FromMilliseconds(100);

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // await peerService.RefreshAsync(refreshLifespan, cancellationToken);
        // await peerService.CheckReplacementCacheAsync(cancellationToken);
    }
}
