using Libplanet.Data;
using Libplanet.TestUtilities;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Xunit.Sdk;

namespace Libplanet.Tests.Blockchain;

public sealed class StagedTransactionCollectionTest(ITestOutputHelper output)
{
    [Fact]
    public void AddTransaction()
    {
        var random = RandomUtility.GetRandom(output);
        var transactionOptions = new TransactionOptions
        {
            LifeTime = TimeSpan.FromSeconds(1),
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, transactionOptions);
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
        var transactionOptions = new TransactionOptions
        {
            LifeTime = TimeSpan.FromSeconds(1),
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, transactionOptions);
        var signer = RandomUtility.Signer(random);
        var tx = new TransactionMetadata
        {
            Signer = signer.Address,
        }.Sign(signer);
        repository.Nonces.Increase(signer.Address, 100);
        Assert.Throws<ArgumentException>(() => transactions.Add(tx));
    }
}
