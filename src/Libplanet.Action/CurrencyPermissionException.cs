using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;

namespace Libplanet.Action;

public sealed class CurrencyPermissionException(string message, Address transactionSigner, Currency currency)
    : UnauthorizedAccessException(message)
{
    public Address TransactionSigner { get; } = transactionSigner;

    public Currency Currency { get; } = currency;
}
