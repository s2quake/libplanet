using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Serilog;

namespace Libplanet.Store;

public sealed class BlockCollection(IDictionary<KeyBytes, byte[]> dictionary)
{
    public Block this[BlockHash blockHash]
    {
        get
        {
            throw new NotImplementedException(
                "This method is not implemented. Use GetBlockDigest instead.");
        }
    }

    public long GetHeight(BlockHash blockHash)
        => throw new NotImplementedException(
            "This method is not implemented. Use GetBlockDigest instead.");

    public BlockDigest GetBlockDigest(BlockHash blockHash)
        => throw new NotImplementedException(
            "This method is not implemented. Use GetBlockDigest instead.");

    public void Add(Block block)
        => throw new NotImplementedException();

    public bool Remove(BlockHash blockHash) => throw new NotImplementedException();

    public bool Contains(BlockHash blockHash) => throw new NotImplementedException();

}
