using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed class BlockChainStates(IStore store, IStateStore stateStore)
{
    private readonly ActivitySource _activitySource = new("Libplanet.Blockchain.BlockChainStates");

    public World GetWorld(BlockHash blockHash)
    {
        using Activity? a = _activitySource
            .StartActivity(ActivityKind.Internal)?
            .AddTag("BlockHash", blockHash.ToString());
        return World.Create(GetTrie(blockHash), stateStore);
    }

    public World GetWorld(HashDigest<SHA256> stateRootHash) => World.Create(GetTrie(stateRootHash), stateStore);

    private ITrie GetTrie(BlockHash blockHash)
    {
        using Activity? a = _activitySource
            .StartActivity(ActivityKind.Internal)?
            .AddTag("BlockHash", blockHash.ToString());
        if (store.GetStateRootHash(blockHash) is { } stateRootHash)
        {
            a?.SetStatus(ActivityStatusCode.Ok);
            return GetTrie(stateRootHash);
        }
        else
        {
            a?.SetStatus(ActivityStatusCode.Error);
            throw new ArgumentException(
                $"Could not find block hash {blockHash} in {nameof(IStore)}.",
                nameof(blockHash));
        }
    }

    private ITrie GetTrie(HashDigest<SHA256> stateRootHash)
    {
        ITrie trie = stateStore.GetStateRoot(stateRootHash);
        if (!trie.IsCommitted)
        {
            throw new ArgumentException(
                $"Could not find state root {stateRootHash} in {nameof(IStateStore)}.",
                nameof(stateRootHash));
        }

        return trie;
    }
}
