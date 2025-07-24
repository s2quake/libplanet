using System.Runtime.CompilerServices;
using System.Threading;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Services;

public sealed class BlockFetcher(
    Blockchain blockchain, ITransport transport)
    : FetcherBase<BlockHash, Block>
{
    public BlockFetcher(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport)
    {
    }

    public override async IAsyncEnumerable<Block> FetchAsync(
        Peer peer, BlockHash[] ids, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
        var request = new BlockRequestMessage { BlockHashes = [.. ids] };
        var predicate = new Func<BlockResponseMessage, bool>(m => m.IsLast);
        var query = transport.SendAsync<BlockResponseMessage>(
            peer, request, predicate, cancellationTokenSource.Token);
        await foreach (var item in query)
        {
            foreach (var block in item.Blocks)
            {
                yield return block;
            }
        }
    }

    protected override HashSet<BlockHash> GetRequiredIds(IEnumerable<BlockHash> ids)
    {
        var blocks = blockchain.Blocks;
        var query = from id in ids
                    where !blocks.ContainsKey(id)
                    select id;

        return [.. query];
    }

    protected override bool Verify(Block item)
    {
        return true;
        // var transactionOptions = blockchain.Options.BlockOptions;
        // var stageBlocks = blockchain.StagedBlocks;

        // try
        // {
        //     transactionOptions.Validate(item);
        //     if (!stageBlocks.ContainsKey(item.Id))
        //     {
        //         stageBlocks.Add(item);
        //         return true;
        //     }
        // }
        // catch
        // {
        //     stageBlocks.Remove(item.Id);
        // }

        // return false;
    }
}
