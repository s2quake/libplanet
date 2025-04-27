using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action.State;

public sealed class CurrencyPermissionException(string message, Address transactionSigner, Currency currency)
    : Exception(message)
{
    public Address TransactionSigner { get; } = transactionSigner;

    public Currency Currency { get; } = currency;
}
