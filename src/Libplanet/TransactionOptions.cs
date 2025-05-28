using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionOptions
{
    public IValidator<Transaction> Validator { get; init; } = new RelayValidator<Transaction>();

    public TimeSpan LifeTime { get; init; } = TimeSpan.FromSeconds(10);

    public IComparer<Transaction> Comparer { get; init; } = new TransactionComparer();

    public void Validate(Transaction transaction)
    {
        Validator.Validate(transaction);
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
