using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Libplanet.Net;

public abstract class FetcherBase<TId, TItem> : IDisposable
    where TId : notnull
    where TItem : notnull
{
    private readonly ConcurrentDictionary<Peer, Job> _jobs = new();
    private readonly List<IDisposable> _subscriptionList = [];
    private readonly Subject<(Guid, ImmutableArray<TItem>)> _fetchedSubject = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public IObservable<(Guid, ImmutableArray<TItem>)> Fetched => _fetchedSubject;

    protected bool IsDisposed { get; private set; }

    protected CancellationToken DisposalToken => _cancellationTokenSource.Token;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Guid Request(Peer peer, ImmutableArray<TId> ids)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (ids.IsDefaultOrEmpty)
        {
            throw new ArgumentException("IDs cannot be empty.", nameof(ids));
        }

        var request = new JobRequest
        {
            RequestId = Guid.NewGuid(),
            Ids = ids,
            CancellationToken = default,
        };
        RequestJob(peer, request);
        return request.RequestId;
    }

    public async Task<ImmutableArray<TItem>> FetchAsync(
        Peer peer, ImmutableArray<TId> ids, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (ids.IsDefaultOrEmpty)
        {
            throw new ArgumentException("IDs cannot be empty.", nameof(ids));
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationTokenSource.Token, cancellationToken);

        var tcs = new TaskCompletionSource<ImmutableArray<TItem>>();
        var request = new JobRequest
        {
            RequestId = Guid.NewGuid(),
            Ids = ids,
            CancellationToken = cancellationTokenSource.Token,
        };
        using var _ = Fetched.Subscribe(
            onNext: e =>
            {
                if (e.Item1 == request.RequestId)
                {
                    tcs.SetResult(e.Item2);
                }
            },
            onError: e =>
            {
                if (e is FetchException fetchException && fetchException.RequestId == request.RequestId)
                {
                    tcs.SetException(fetchException);
                }
            });

        RequestJob(peer, request);
        return await tcs.Task;
    }

    protected abstract IAsyncEnumerable<TItem> FetchOverrideAsync(
        Peer peer, ImmutableArray<TId> ids, CancellationToken cancellationToken);

    protected abstract bool Verify(TItem item);

    protected abstract bool Predicate(TId ids);

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _cancellationTokenSource.Cancel();
                foreach (var subscription in _subscriptionList)
                {
                    subscription.Dispose();
                }

                _jobs.Clear();
                _cancellationTokenSource.Dispose();
            }

            IsDisposed = true;
        }
    }

    private void ProcessJobResponse(JobResponse response)
    {
        var itemList = new List<TItem>(response.Items.Length);
        foreach (var item in response.Items)
        {
            if (Verify(item))
            {
                itemList.Add(item);
            }
        }

        _fetchedSubject.OnNext((response.RequestId, itemList.ToImmutableArray()));
    }

    private void RequestJob(Peer peer, JobRequest request)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_jobs.TryAdd(peer, new Job(this, peer)))
        {
            var job = _jobs[peer];
            var subscription = job.Completed.Subscribe(
                onNext: ProcessJobResponse,
                onError: _fetchedSubject.OnError);
            _ = job.RunAsync(_cancellationTokenSource.Token);
            _subscriptionList.Add(subscription);
        }

        _jobs[peer].Request(request with
        {
            Ids = [.. request.Ids.Where(Predicate)]
        });
    }

    private sealed class Job(FetcherBase<TId, TItem> fetcher, Peer peer) : IDisposable
    {
        private readonly Subject<JobResponse> _fetchedSubject = new();
        private readonly Channel<JobRequest> _channel = Channel.CreateUnbounded<JobRequest>();

        public IObservable<JobResponse> Completed => _fetchedSubject;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, request.CancellationToken);
                    var fetchCancellationToken = cancellationTokenSource.Token;
                    var query = fetcher.FetchOverrideAsync(peer, request.Ids, fetchCancellationToken);
                    var items = await query.ToArrayAsync(fetchCancellationToken);
                    _fetchedSubject.OnNext(new JobResponse
                    {
                        RequestId = request.RequestId,
                        Items = [.. items],
                    });
                }
                catch (Exception e) when (!cancellationToken.IsCancellationRequested)
                {
                    _fetchedSubject.OnError(new FetchException(request.RequestId, "Failed to fetch items.", e));
                }
            }
        }

        public void Request(JobRequest request)
        {
            _channel.Writer.TryWrite(request);
        }

        public void Dispose()
        {
            _channel.Writer.Complete();
            _fetchedSubject.Dispose();
        }
    }

    private sealed record class JobRequest
    {
        public required Guid RequestId { get; init; }

        public required ImmutableArray<TId> Ids { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed record class JobResponse
    {
        public required Guid RequestId { get; init; }

        public required ImmutableArray<TItem> Items { get; init; } = [];
    }
}
