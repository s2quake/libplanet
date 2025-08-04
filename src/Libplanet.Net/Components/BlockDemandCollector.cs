using System.Runtime.CompilerServices;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

[Obsolete("Use BlockDemandCollector from Libplanet.Net.Services instead.")]
public sealed class BlockDemandCollector(Blockchain blockchain, ITransport transport)
{
    public BlockDemandCollection BlockDemands { get; } = new();

    public TimeSpan BlockDemandLifespan { get; init; } = TimeSpan.FromMinutes(1);

    public async Task ExecuteAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        var blockchainStates = GetBlockchainStateAsync(peers, cancellationToken);
        await foreach (var blockchainState in blockchainStates)
        {
            var blockDemand = new BlockDemand(blockchainState.Peer, blockchainState.Tip, DateTimeOffset.UtcNow);
            AddOrUpdate(blockDemand);
        }
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

    private void AddOrUpdate(BlockDemand blockDemand)
    {
        if (IsDemandNeeded(blockDemand))
        {
            BlockDemands.AddOrUpdate(blockDemand);
        }
    }

    private bool IsDemandNeeded(BlockDemand blockDemand)
    {
        if (blockDemand.IsStale(BlockDemandLifespan))
        {
            return false;
        }

        if (blockDemand.Height <= blockchain.Tip.Height)
        {
            return false;
        }

        if (BlockDemands.TryGetValue(blockDemand.Peer, out var oldBlockDemand))
        {
            return oldBlockDemand.IsStale(BlockDemandLifespan) || oldBlockDemand.Height < blockDemand.Height;
        }

        return true;
    }
}
