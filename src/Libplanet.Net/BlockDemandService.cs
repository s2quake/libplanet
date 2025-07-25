using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class BlockDemandService(Blockchain blockchain, ITransport transport) : ServiceBase
{
    public BlockDemandCollection BlockDemands { get; } = new(blockchain);

    public async Task ExecuteAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var blockchainStates = GetBlockchainStateAsync(peers, cancellationTokenSource.Token);
        await foreach (var blockchainState in blockchainStates)
        {
            var blockDemand = new BlockDemand(blockchainState.Peer, blockchainState.Tip, DateTimeOffset.UtcNow);
            BlockDemands.AddOrUpdate(blockDemand);
        }
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async IAsyncEnumerable<BlockchainState> GetBlockchainStateAsync(
        ImmutableArray<Peer> peers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var peer in peers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = transport.GetBlockchainStateAsync(peer, cancellationToken);
            if (await TaskUtility.TryWait(task))
            {
                yield return Create(peer, task.Result);
            }
        }

        static BlockchainState Create(Peer peer, BlockchainStateResponseMessage message)
            => new(peer, message.Genesis, message.Tip);
    }
}
