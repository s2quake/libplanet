using System.Security.Cryptography;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Store;

public static class StoreExtensions
{
    public static void Copy(this Repository from, Repository to)
    {
        // if (to.Chains.Keys.Any())
        // {
        //     throw new ArgumentException("The destination store has to be empty.", nameof(to));
        // }

        // var fromBlocks = from.Blocks;
        // var toBlocks = to.Blocks;
        // foreach (Guid chainId in from.Chains.Keys.ToArray())
        // {
        //     foreach (BlockHash blockHash in from.GetBlockHashes(chainId).IterateIndexes())
        //     {
        //         var block = fromBlocks[blockHash];
        //         toBlocks.Add(block);
        //         // to.AppendIndex(chainId, blockHash);
        //     }

        //     foreach (KeyValuePair<Address, long> kv in from.GetNonceCollection(chainId))
        //     {
        //         to.GetNonceCollection(chainId).Increase(kv.Key, kv.Value);
        //     }
        // }

        // if (from.ChainId is Guid canonId)
        // {
        //     to.ChainId = canonId;
        // }
    }

    public static HashDigest<SHA256> GetStateRootHash(this Repository store, BlockHash blockHash)
        => store.BlockDigests[blockHash].StateRootHash;
}
