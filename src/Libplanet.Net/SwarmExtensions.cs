using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;
using System.Runtime.CompilerServices;

namespace Libplanet.Net;

public static class SwarmExtensions
{
    public static async IAsyncEnumerable<BlockchainState> GetBlockchainStateAsync(
        this Swarm @this,
        TimeSpan dialTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var peers = @this.Peers.ToArray();
        var random = new Random();
        var transport = @this.Transport;
        random.Shuffle(peers);

        foreach (var peer in peers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = transport.GetBlockchainStateAsync(peer, cancellationToken);
            var waitTask = task.WaitAsync(dialTimeout, cancellationToken);
            if (await TaskUtility.TryWait(waitTask))
            {
                yield return Create(peer, waitTask.Result);
            }
        }

        static BlockchainState Create(Peer peer, BlockchainStateResponseMessage message)
            => new(peer, message.Genesis, message.Tip);
    }

    public static async IAsyncEnumerable<(Peer, BlockSummary)> GetPeersWithBlockSummary(
        this Swarm @this,
        TimeSpan dialTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var blockchain = @this.Blockchain;
        await foreach (var blockchainState in GetBlockchainStateAsync(@this, dialTimeout, cancellationToken))
        {
            if (blockchainState.Genesis.BlockHash == blockchain.Genesis.BlockHash &&
                blockchainState.Tip.Height > blockchain.Tip.Height)
            {
                yield return (blockchainState.Peer, blockchainState.Tip);
            }
        }
    }
}
