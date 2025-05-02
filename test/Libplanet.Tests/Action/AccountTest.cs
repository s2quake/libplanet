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
            .SetValue(_addr[0], "a")
            .SetValue(_addr[1], "b");
    }

    [Fact]
    public void NullDelta()
    {
        Assert.Equal("a", (string)_initAccount.GetValue(_addr[0]));
        Assert.Equal("b", (string)_initAccount.GetValue(_addr[1]));
        Assert.Null(_initAccount.GetValueOrDefault(_addr[2]));
    }

    [Fact]
    public void States()
    {
        Account a = _initAccount.SetValue(_addr[0], "A");
        AccountDiff diffa = AccountDiff.Create(_initAccount.Trie, a.Trie);
        Assert.Equal("A", a.GetValue(_addr[0]));
        Assert.Equal("a", _initAccount.GetValue(_addr[0]));
        Assert.Equal("b", a.GetValue(_addr[1]));
        Assert.Equal("b", _initAccount.GetValue(_addr[1]));
        Assert.Null(a.GetValueOrDefault(_addr[2]));
        Assert.Null(_initAccount.GetValueOrDefault(_addr[2]));
        Assert.Equal(_addr[0], Assert.Single(diffa.StateDiffs).Key);

        Account b = a.SetValue(_addr[0], "z");
        AccountDiff diffb = AccountDiff.Create(a.Trie, b.Trie);
        Assert.Equal("z", b.GetValue(_addr[0]));
        Assert.Equal("A", a.GetValue(_addr[0]));
        Assert.Equal("a", _initAccount.GetValue(_addr[0]));
        Assert.Equal("b", b.GetValue(_addr[1]));
        Assert.Equal("b", a.GetValue(_addr[1]));
        Assert.Null(b.GetValueOrDefault(_addr[2]));
        Assert.Null(a.GetValueOrDefault(_addr[2]));
        Assert.Equal(_addr[0], Assert.Single(diffb.StateDiffs).Key);

        Account c = b.SetValue(_addr[0], "a");
        Assert.Equal("a", c.GetValue(_addr[0]));
        Assert.Equal("z", b.GetValue(_addr[0]));
    }

    [Fact]
    public void RemoveState()
    {
        Account a = _initAccount.SetValue(_addr[0], "A");
        a = a.SetValue(_addr[1], "B");
        Assert.Equal("A", a.GetValue(_addr[0]));
        Assert.Equal("B", a.GetValue(_addr[1]));

        a = a.RemoveValue(_addr[0]);
        Assert.Null(a.GetValueOrDefault(_addr[0]));
        Assert.Equal("B", a.GetValue(_addr[1]));

        a = a.RemoveValue(_addr[1]);
        Assert.Null(a.GetValueOrDefault(_addr[0]));
        Assert.Null(a.GetValueOrDefault(_addr[1]));
    }
}
