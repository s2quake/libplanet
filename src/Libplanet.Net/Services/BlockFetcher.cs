using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class BlockFetcher(Blockchain blockchain, ITransport transport)
    : FetcherBase<BlockHash, (Block, BlockCommit)>
{
    public BlockFetcher(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport)
    {
    }

    public async Task<ImmutableArray<(Block, BlockCommit)>> FetchAsync(
        Peer peer, BlockHash branchPoint, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var blockHashes = await transport.GetBlockHashesAsync(peer, branchPoint, cancellationTokenSource.Token);
        if (blockHashes.Length is 0)
        {
            return [];
        }

        return await FetchAsync(peer, blockHashes, cancellationTokenSource.Token);
    }

    protected override async IAsyncEnumerable<(Block, BlockCommit)> FetchOverrideAsync(
        Peer peer, ImmutableArray<BlockHash> ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var request = new BlockRequestMessage { BlockHashes = ids };
        var isLast = new Func<BlockResponseMessage, bool>(m => m.IsLast);
        var query = transport.SendAsync(peer, request, isLast, cancellationTokenSource.Token);
        await foreach (var item in query)
        {
            for (var i = 0; i < item.Blocks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (item.Blocks[i], item.BlockCommits[i]);
            }
        }
    }

    protected override bool Predicate(BlockHash id) => !blockchain.Blocks.ContainsKey(id);

    protected override bool Verify((Block, BlockCommit) item)
    {
        return true;
    }
}
