using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action.Tests.Common;

/// <summary>
/// A set of variety of <see cref="Currency"><c>Currencies</c></see> used for testing.
/// As minter's identities are dynamically generated at runtime, any <see cref="Currency"/>
/// with non-<see langword="null"/> <see cref="Currency.Minters"/> specified has variable
/// <see cref="Currency.Hash"/>, namely <see cref="CurrencyC"/>, <see cref="CurrencyD"/>,
/// <see cref="CurrencyE"/>, and <see cref="CurrencyF"/>.
/// </summary>
public static class Currencies
{
    /// <summary>
    /// The <see cref="PrivateKey"/> of a minter for <see cref="CurrencyC"/>,
    /// <see cref="CurrencyD"/>, <see cref="CurrencyE"/>, and <see cref="CurrencyF"/>.
    /// </summary>
    public static readonly PrivateKey MinterAKey = new PrivateKey();

    /// <summary>
    /// The <see cref="PrivateKey"/> of a minter for <see cref="CurrencyD"/>.
    /// </summary>
    public static readonly PrivateKey MinterBKey = new PrivateKey();

    /// <summary>
    /// The <see cref="Address"/> of a minter for <see cref="CurrencyC"/>,
    /// <see cref="CurrencyD"/>, <see cref="CurrencyE"/>, and <see cref="CurrencyF"/>.
    /// </summary>
    public static readonly Address MinterA = MinterAKey.Address;

    /// <summary>
    /// The <see cref="Address"/> of a minter for <see cref="CurrencyD"/>.
    /// </summary>
    public static readonly Address MinterB = MinterBKey.Address;

    /// <summary>
    /// A simple legacy <see cref="Currency"/> with ticker "AAA", no decimal places, and
    /// no minters.
    /// </summary>
    public static readonly Currency CurrencyA = new("AAA", 0);

    /// <summary>
    /// A simple legacy <see cref="Currency"/> with ticker "BBB", two decimal places, and
    /// no minters.
    /// </summary>
    public static readonly Currency CurrencyB = new("BBB", 2);

    /// <summary>
    /// A simple legacy <see cref="Currency"/> with ticker "CCC", no decimal places, and
    /// <see cref="MinterA"/> as its minter.
    /// </summary>
    public static readonly Currency CurrencyC = new("CCC", 0, [MinterA]);

    /// <summary>
    /// A simple legacy <see cref="Currency"/> with ticker "DDD", no decimal places, and
    /// <see cref="MinterA"/> and <see cref="MinterB"/> as its minters.
    /// </summary>
    public static readonly Currency CurrencyD = new("DDD", 0, [MinterA, MinterB]);

    /// <summary>
    /// A simple uncapped <see cref="Currency"/> with ticker "EEE", no decimal places, and
    /// <see cref="MinterA"/> as its minter.
    /// </summary>
    public static readonly Currency CurrencyE = new("EEE", 0, [MinterA]);

    /// <summary>
    /// A simple uncapped <see cref="Currency"/> with ticker "FFF", no decimal places, and
    /// <see cref="MinterA"/> as its minter.
    /// </summary>
    public static readonly Currency CurrencyF = new("FFF", 0, 100, [MinterA]);
}
