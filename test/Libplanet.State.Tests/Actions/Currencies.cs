using Libplanet.Types;

namespace Libplanet.State.Tests.Actions;

public static class Currencies
{
    public static readonly ISigner MinterA = new PrivateKey().AsSigner();

    public static readonly ISigner MinterB = new PrivateKey().AsSigner();

    public static readonly Currency CurrencyA = Currency.Create("AAA", 0);

    public static readonly Currency CurrencyB = Currency.Create("BBB", 2);

    public static readonly Currency CurrencyC = Currency.Create("CCC", 0, [MinterA.Address]);

    public static readonly Currency CurrencyD = Currency.Create("DDD", 0, [MinterA.Address, MinterB.Address]);

    public static readonly Currency CurrencyE = Currency.Create("EEE", 0, [MinterA.Address]);

    public static readonly Currency CurrencyF = Currency.Create("FFF", 0, 100, [MinterA.Address]);
}
