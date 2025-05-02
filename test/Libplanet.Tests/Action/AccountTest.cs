using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Xunit.Abstractions;

namespace Libplanet.Tests.Action;

public sealed class AccountTest
{
    private readonly PrivateKey[] _keys;
    private readonly Address[] _addr;
    private readonly Account _initAccount;

    public AccountTest(ITestOutputHelper output)
    {
        _keys =
        [
            new PrivateKey(),
            new PrivateKey(),
            new PrivateKey(),
        ];

        _addr = _keys.Select(key => key.Address).ToArray();
        _initAccount = new Account()
            .SetState(_addr[0], "a")
            .SetState(_addr[1], "b");
    }

    [Fact]
    public void NullDelta()
    {
        Assert.Equal("a", (string)_initAccount.GetState(_addr[0]));
        Assert.Equal("b", (string)_initAccount.GetState(_addr[1]));
        Assert.Null(_initAccount.GetStateOrDefault(_addr[2]));
    }

    [Fact]
    public void States()
    {
        Account a = _initAccount.SetState(_addr[0], "A");
        AccountDiff diffa = AccountDiff.Create(_initAccount.Trie, a.Trie);
        Assert.Equal("A", a.GetState(_addr[0]));
        Assert.Equal("a", _initAccount.GetState(_addr[0]));
        Assert.Equal("b", a.GetState(_addr[1]));
        Assert.Equal("b", _initAccount.GetState(_addr[1]));
        Assert.Null(a.GetStateOrDefault(_addr[2]));
        Assert.Null(_initAccount.GetStateOrDefault(_addr[2]));
        Assert.Equal(_addr[0], Assert.Single(diffa.StateDiffs).Key);

        Account b = a.SetState(_addr[0], "z");
        AccountDiff diffb = AccountDiff.Create(a.Trie, b.Trie);
        Assert.Equal("z", b.GetState(_addr[0]));
        Assert.Equal("A", a.GetState(_addr[0]));
        Assert.Equal("a", _initAccount.GetState(_addr[0]));
        Assert.Equal("b", b.GetState(_addr[1]));
        Assert.Equal("b", a.GetState(_addr[1]));
        Assert.Null(b.GetStateOrDefault(_addr[2]));
        Assert.Null(a.GetStateOrDefault(_addr[2]));
        Assert.Equal(_addr[0], Assert.Single(diffb.StateDiffs).Key);

        Account c = b.SetState(_addr[0], "a");
        Assert.Equal("a", c.GetState(_addr[0]));
        Assert.Equal("z", b.GetState(_addr[0]));
    }

    [Fact]
    public void RemoveState()
    {
        Account a = _initAccount.SetState(_addr[0], "A");
        a = a.SetState(_addr[1], "B");
        Assert.Equal("A", a.GetState(_addr[0]));
        Assert.Equal("B", a.GetState(_addr[1]));

        a = a.RemoveState(_addr[0]);
        Assert.Null(a.GetStateOrDefault(_addr[0]));
        Assert.Equal("B", a.GetState(_addr[1]));

        a = a.RemoveState(_addr[1]);
        Assert.Null(a.GetStateOrDefault(_addr[0]));
        Assert.Null(a.GetStateOrDefault(_addr[1]));
    }
}
