using Libplanet.Types;

namespace Libplanet.State;

public sealed class CurrencyPermissionException(string message, Address transactionSigner, Currency currency)
    : UnauthorizedAccessException(message)
{
    public Address TransactionSigner { get; } = transactionSigner;

    public Currency Currency { get; } = currency;
}
