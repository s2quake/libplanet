using Libplanet.Store;

namespace Libplanet.Action.State;

internal static class StateStoreExtensions
{
    public static World CommitWorld(this StateStore stateStore, World world)
    {
        var trie = world.Trie;
        foreach (var (name, account) in world.Delta)
        {
            var accountTrie = stateStore.Commit(account.Trie);
            var key = name;
            var value = accountTrie.Hash.Bytes;
            trie = trie.Set(key, value);
        }

        return new World(stateStore.Commit(trie), stateStore);
    }
}
