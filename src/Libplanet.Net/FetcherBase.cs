#pragma warning disable S2743 // Static fields should not be used in generic types
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public abstract class FetcherBase<TId, TItem> : ServiceBase
    where TId : notnull
    where TItem : notnull
{
    private readonly ConcurrentDictionary<Peer, Job> _jobs = new();
    private readonly List<IDisposable> _subscriptionList = [];
    private readonly Subject<ReceivedInfo<TItem>> _receivedSubject = new();

    public IObservable<ReceivedInfo<TItem>> Received => _receivedSubject;

    public void DemandMany(Peer peer, TId[] ids)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var requiredIds = GetRequiredIds(ids);
        var cancellationToken = StoppingToken;

        if (_jobs.TryAdd(peer, new Job(this, peer)))
        {
            var job = _jobs[peer];
            var subscription = job.Fetched.Subscribe(e => ProcessFetchedTIds(e, peer));
            _ = job.RunAsync(cancellationToken);
            _subscriptionList.Add(subscription);
        }

        _jobs[peer].Add(requiredIds);
    }

    public abstract IAsyncEnumerable<TItem> FetchAsync(Peer peer, TId[] ids, CancellationToken cancellationToken);

    protected abstract bool Verify(TItem item);

    protected abstract HashSet<TId> GetRequiredIds(IEnumerable<TId> ids);

    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async ValueTask DisposeAsyncCore()
    {
        foreach (var subscription in _subscriptionList)
        {
            subscription.Dispose();
        }

        _jobs.Clear();
        await base.DisposeAsyncCore();
    }

    private void ProcessFetchedTIds(HashSet<TItem> items, Peer peer)
    {
        var itemList = new List<TItem>(items.Count);
        foreach (var tx in items)
        {
            if (Verify(tx))
            {
                itemList.Add(tx);
            }
        }

        if (itemList.Count > 0)
        {
            _receivedSubject.OnNext(new(peer, [.. itemList]));
        }
    }

    private sealed class Job(FetcherBase<TId, TItem> fetcher, Peer peer)
    {
        private static readonly object _lock = new();
        private readonly List<TId> _idList = [];
        private readonly Subject<HashSet<TItem>> _fetchedSubject = new();

        public IObservable<HashSet<TItem>> Fetched => _fetchedSubject;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            do
            {
                var txs = new HashSet<TItem>();
                var txIds = Flush();
                await foreach (var item in fetcher.FetchAsync(peer, txIds, cancellationToken))
                {
                    txs.Add(item);
                }

                if (txs.Count > 0)
                {
                    _fetchedSubject.OnNext(txs);
                }
            } while (await TaskUtility.TryDelay(1000, cancellationToken));
        }

        private TId[] Flush()
        {
            lock (_lock)
            {
                var ids = _idList.ToArray();
                _idList.Clear();
                return ids;
            }
        }

        public void Add(IEnumerable<TId> ids)
        {
            lock (_lock)
            {
                _idList.AddRange(ids);
            }
        }
    }
}
