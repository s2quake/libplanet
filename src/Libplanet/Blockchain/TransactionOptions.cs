using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed record class TransactionOptions
{
    public IValidator<Transaction> Validator { get; init; } = new RelayValidator<Transaction>();

    public TimeSpan LifeTime { get; init; } = TimeSpan.FromSeconds(10);

    public void Validate(Transaction transaction)
    {
        Validator.Validate(transaction);
    }
}
