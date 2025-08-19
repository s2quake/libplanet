using Libplanet.State;
using Libplanet.Types;

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
