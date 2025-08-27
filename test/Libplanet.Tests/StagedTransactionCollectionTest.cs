using Libplanet.Data;
using Libplanet.TestUtilities;
using Libplanet.Types;

namespace Libplanet.Tests;

public sealed class StagedTransactionCollectionTest(ITestOutputHelper output)
{
    [Fact]
    public void AddTransaction()
    {
        var random = RandomUtility.GetRandom(output);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Lifetime = TimeSpan.FromSeconds(1),
            },
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, options);
        var signer = RandomUtility.Signer(random);
        var tx = new TransactionMetadata
        {
            Signer = signer.Address,
        }.Sign(signer);
        transactions.Add(tx);
        Assert.Contains(tx.Id, transactions);
    }

    [Fact]
    public void AddTransactionWithExpiredNonce()
    {
        var random = RandomUtility.GetRandom(output);
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Lifetime = TimeSpan.FromSeconds(1),
            },
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, options);
        var signer = RandomUtility.Signer(random);
        var tx = new TransactionMetadata
        {
            Signer = signer.Address,
        }.Sign(signer);
        repository.Nonces.Increase(signer.Address, 100);
        Assert.Throws<ArgumentException>(() => transactions.Add(tx));
    }
}
