using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionOptions
{
    public ImmutableArray<IObjectValidator<Transaction>> Validators { get; init; } = [];

    public TimeSpan LifeTime { get; init; } = TimeSpan.FromSeconds(10);

    public IComparer<Transaction> Comparer { get; init; } = new TransactionComparer();

    internal void Validate(Transaction transaction)
    {
        if (transaction.Timestamp + LifeTime < DateTimeOffset.UtcNow)
        {
            throw new ArgumentException($"Transaction {transaction.Id} is expired.", nameof(transaction));
        }

        foreach (var validator in Validators)
        {
            validator.Validate(transaction);
        }
    }

    private sealed class TransactionComparer : IComparer<Transaction>
    {
        public int Compare(Transaction? x, Transaction? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            return x.Id.CompareTo(y.Id);
        }
    }
}
