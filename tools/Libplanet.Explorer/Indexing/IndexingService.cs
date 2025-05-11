using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Libplanet.Explorer.Indexing;

/// <summary>
/// An ASP.NET Core service that indexes blocks added to the given <see cref="Libplanet.Store.Store"/>
/// instance to the provided <see cref="IBlockChainIndex"/> instance.
/// </summary>
public class IndexingService : BackgroundService
{
    private readonly IBlockChainIndex _index;
    private readonly Libplanet.Store.Store _store;
    private readonly TimeSpan _pollInterval;

    /// <summary>
    /// Create an instance of the service that indexes blocks added to the <paramref name="chain"/>
    /// to the <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index object that blocks will be indexed.</param>
    /// <param name="store">The <see cref="Libplanet.Store.Store"/> object that will be indexed by the
    /// <paramref name="index"/>.</param>
    /// <param name="pollInterval">The interval between index synchronization polls in
    /// <see cref="TimeSpan"/>. Recommended value is about the same as block interval.</param>
    public IndexingService(IBlockChainIndex index, Libplanet.Store.Store store, TimeSpan pollInterval)
    {
        _index = index;
        _store = store;
        _pollInterval = pollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _index.SynchronizeForeverAsync(_store, _pollInterval, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
