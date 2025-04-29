using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action.Tests.Common;

public static class Currencies
{
    public static readonly PrivateKey MinterAKey = new PrivateKey();

    public static readonly PrivateKey MinterBKey = new PrivateKey();

    public static readonly Address MinterA = MinterAKey.Address;

    public static readonly Address MinterB = MinterBKey.Address;

    public static readonly Currency CurrencyA = Currency.Create("AAA", 0);

    public static readonly Currency CurrencyB = Currency.Create("BBB", 2);

    public static readonly Currency CurrencyC = Currency.Create("CCC", 0, [MinterA]);

    public static readonly Currency CurrencyD = Currency.Create("DDD", 0, [MinterA, MinterB]);

    public static readonly Currency CurrencyE = Currency.Create("EEE", 0, [MinterA]);

    public static readonly Currency CurrencyF = Currency.Create("FFF", 0, 100, [MinterA]);
}
