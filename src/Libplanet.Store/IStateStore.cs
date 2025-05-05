using System.Security.Cryptography;
using Libplanet.Store.Trie;
using Libplanet.Types;

namespace Libplanet.Store;

public interface IStateStore : IDisposable
{
    ITrie GetStateRoot(HashDigest<SHA256> stateRootHash);

    ITrie Commit(ITrie trie);
}
