using Libplanet.Types;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components;

public static class PeerExplorerExtensions
{
    public static void Broadcast(this PeerExplorer @this, BlockHash genesisHash, Block block)
    {
        var message = new BlockSummaryMessage
        {
            GenesisHash = genesisHash,
            BlockSummary = block,
        };
        @this.Broadcast(message);
    }
}
