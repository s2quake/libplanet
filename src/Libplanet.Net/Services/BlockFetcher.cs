using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class BlockFetcher(Blockchain blockchain, ITransport transport)
    : FetcherBase<BlockHash, Block>
{
    public BlockFetcher(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport)
    {
    }

    protected override async IAsyncEnumerable<Block> FetchOverrideAsync(
        Peer peer, ImmutableArray<BlockHash> ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var request = new BlockRequestMessage { BlockHashes = ids };
        var isLast = new Func<BlockResponseMessage, bool>(m => m.IsLast);
        var query = transport.SendAsync(peer, request, isLast, cancellationTokenSource.Token);
        await foreach (var item in query)
        {
            foreach (var block in item.Blocks)
            {
                yield return block;
            }
        }
    }

    protected override bool Predicate(BlockHash id) => !blockchain.Blocks.ContainsKey(id);

    protected override bool Verify(Block item)
    {
        return true;
    }
}
