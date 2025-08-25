using Libplanet.Types;

namespace Libplanet;

public sealed record class BlockOptions
{
    public long MaxActionBytes { get; init; } = 100L * 1024L;

    public int MinTransactions { get; init; } = 0;

    public int MaxTransactions { get; init; } = 100;

    public int MaxTransactionsPerSigner { get; init; } = 100;

    public long EvidencePendingDuration { get; init; } = 10L;

    public int MaxEvidence { get; init; } = 10;

    public ImmutableArray<IObjectValidator<Block>> Validators { get; init; } = [];

    public void Validate(Block block)
    {
        foreach (var validator in Validators)
        {
            validator.Validate(block);
        }

        var actionBytes = block.Transactions.SelectMany(item => item.Actions)
            .Select(item => item.Bytes.Length)
            .Aggregate(0, (s, i) => s + i);

        if (actionBytes > MaxActionBytes)
        {
            throw new ArgumentException(
                $"The size of block #{block.Height} {block.BlockHash} " +
                $"actions ({actionBytes} bytes) exceeds the limit of {MaxActionBytes} bytes.");
        }
        else if (block.Transactions.Count < MinTransactions)
        {
            throw new ArgumentException(
                $"Block #{block.Height} {block.BlockHash} should include " +
                $"at least {MinTransactions} transaction(s): " +
                $"{block.Transactions.Count}");
        }
        else if (block.Transactions.Count > MaxTransactions)
        {
            throw new ArgumentException(
                $"Block #{block.Height} {block.BlockHash} should include " +
                $"at most {MaxTransactions} transaction(s): " +
                $"{block.Transactions.Count}");
        }
        else
        {
            var query = from tx in block.Transactions
                        group tx by tx.Signer into @group
                        where @group.Count() > MaxTransactionsPerSigner
                        select new { Signer = @group.Key, Count = @group.Count() };
            if (query.FirstOrDefault() is { } item)
            {
                throw new ArgumentException(
                    $"Block #{block.Height} {block.BlockHash} includes " +
                    $"{item.Count} transactions from the same signer {item.Signer}, " +
                    $"which exceeds the limit of {MaxTransactionsPerSigner}.");
            }
        }

        var evidenceExpirationHeight = block.Height - EvidencePendingDuration;
        if (block.Evidences.Any(evidence => evidence.Height < evidenceExpirationHeight))
        {
            throw new ArgumentException(
                $"Block #{block.Height} {block.BlockHash} includes " +
                $"evidences that are expired (height < {evidenceExpirationHeight}).");
        }
    }

    internal bool IsEvidenceExpired(EvidenceBase evidence, int height)
        => evidence.Height + EvidencePendingDuration + evidence.Height < height;
}
