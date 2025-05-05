using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using static Libplanet.Serialization.ModelSerializer;

namespace Libplanet.Tests.Action;

public class AccountDiffTest
{
    [Fact]
    public void EmptyAccountStateSource()
    {
        var stateStore = new TrieStateStore();
        var targetTrie = stateStore.GetStateRoot(default);
        var sourceTrie = stateStore.GetStateRoot(default);

        var diff = AccountDiff.Create(targetTrie, sourceTrie);
        Assert.Empty(diff.StateDiffs);

        var targetAccount = new Account(targetTrie);
        var signer = new PrivateKey();
        targetAccount = targetAccount.SetValue(signer.Address, "Foo");

        targetTrie = stateStore.Commit(targetAccount.Trie);

        diff = AccountDiff.Create(targetTrie, sourceTrie);
        Assert.Empty(diff.StateDiffs);
    }

    [Fact]
    public void Diff()
    {
        var stateStore = new TrieStateStore();
        var targetTrie = stateStore.GetStateRoot(default);
        var sourceTrie = stateStore.GetStateRoot(default);

        var addr1 = RandomUtility.Address();
        var addr2 = RandomUtility.Address();
        var addr3 = RandomUtility.Address();

        AccountDiff diff = AccountDiff.Create(targetTrie, sourceTrie);
        Assert.Empty(diff.StateDiffs);

        var targetAccount = new Account(targetTrie);
        var signer = new PrivateKey();
        targetAccount = targetAccount.SetValue(addr1, "One");
        targetAccount = targetAccount.SetValue(addr2, "Two");
        targetTrie = stateStore.Commit(targetAccount.Trie);

        sourceTrie = targetTrie;

        Account sourceAccount = new Account(sourceTrie);
        sourceAccount = sourceAccount.SetValue(addr2, "Two_");
        sourceAccount = sourceAccount.SetValue(addr3, "Three");

        sourceTrie = stateStore.Commit(sourceAccount.Trie);

        diff = AccountDiff.Create(targetTrie, sourceTrie);
        Assert.Equal(2, diff.StateDiffs.Count);
        Assert.Equal(
            (Serialize("Two"), Serialize("Two_")),
            diff.StateDiffs[addr2]);
        Assert.Equal((null, Serialize("Three")), diff.StateDiffs[addr3]);

        diff = AccountDiff.Create(sourceTrie, targetTrie);
        Assert.Single(diff.StateDiffs);                 // Note addr3 is not tracked
        Assert.Equal(
            (Serialize("Two_"), Serialize("Two")),
            diff.StateDiffs[addr2]);
    }

    public IActionContext CreateActionContext(Address signer, ITrie trie) =>
        new ActionContext
        {
            Signer = signer,
            Proposer = signer,
            BlockProtocolVersion = Block.CurrentProtocolVersion,
        };
}
