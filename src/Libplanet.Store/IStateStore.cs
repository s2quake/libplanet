using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Store.Trie;

namespace Libplanet.Store;

public interface IStateStore : IDisposable
{
    ITrie GetStateRoot(HashDigest<SHA256> stateRootHash);

    ITrie Commit(ITrie trie);
}
