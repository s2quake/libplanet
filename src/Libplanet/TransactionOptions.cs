using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionOptions
{
    public ImmutableArray<IObjectValidator<Transaction>> Validators { get; init; } = [];

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// All transactions must be sorted by their nonce in ascending order.
    /// </remarks>
    public Func<IEnumerable<Transaction>, IEnumerable<Transaction>> Sorter { get; init; }
        = StagedTransactionCollection.Sort;

    internal void Validate(Transaction transaction)
    {
        if (transaction.Timestamp + Lifetime < DateTimeOffset.UtcNow)
        {
            throw new ArgumentException($"Transaction {transaction.Id} is expired.", nameof(transaction));
        }

        foreach (var validator in Validators)
        {
            validator.Validate(transaction);
        }
    }
}
