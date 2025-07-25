using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Services.Extensions;

public static class BlockDemandCollectionExtensions
{
    public static async Task PollAsync(
        this BlockDemandCollection @this,
        ITransport transport,
        ImmutableArray<Peer> peers,
        CancellationToken cancellationToken)
    {
        await foreach (var blockchainState in GetBlockchainStateAsync(transport, [.. peers], cancellationToken))
        {
            var blockDemand = new BlockDemand(blockchainState.Peer, blockchainState.Tip, DateTimeOffset.UtcNow);
            @this.AddOrUpdate(blockDemand);
        }
    }

    private static async IAsyncEnumerable<BlockchainState> GetBlockchainStateAsync(
        ITransport transport,
        Peer[] peers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var random = new Random();
        random.Shuffle(peers);

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
