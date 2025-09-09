using Libplanet.Data;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Tests;

public sealed class StagedTransactionCollectionTest(ITestOutputHelper output)
{
    [Fact]
    public void AddTransaction()
    {
        var random = Rand.GetRandom(output);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Lifetime = TimeSpan.FromSeconds(1),
            },
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, options);
        var signer = Rand.Signer(random);
        var tx = new TransactionMetadata
        {
            Signer = signer.Address,
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(signer);
        transactions.Add(tx);
        Assert.Contains(tx.Id, transactions);
    }

    [Fact]
    public void AddTransactionWithExpiredNonce()
    {
        var random = Rand.GetRandom(output);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Lifetime = TimeSpan.FromSeconds(1),
            },
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, options);
        var signer = Rand.Signer(random);
        var tx = new TransactionMetadata
        {
            Nonce = 0L,
            Signer = signer.Address,
            Timestamp = DateTimeOffset.UtcNow,
        }.Sign(signer);
        repository.Nonces.Increase(signer.Address, 100);
        transactions.Add(tx);
        Assert.Contains(tx.Id, transactions);
    }
}
