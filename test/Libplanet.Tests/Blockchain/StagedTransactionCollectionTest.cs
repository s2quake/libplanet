using Libplanet;
using Libplanet.Data;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet.Tests.Blockchain;

public sealed class StagedTransactionCollectionTest
{
    [Fact]
    public void AddTransaction()
    {
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                LifeTime = TimeSpan.FromSeconds(1),
            },
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, options);
        var privateKey = new PrivateKey();
        var tx = new TransactionMetadata
        {
            Signer = privateKey.Address,
        }.Sign(privateKey);
        transactions.Add(tx);
        Assert.Contains(tx.Id, transactions);
    }

    [Fact]
    public void AddTransactionWithExpiredNonce()
    {
        var options = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                LifeTime = TimeSpan.FromSeconds(1),
            },
        };
        var repository = new Repository();
        var transactions = new StagedTransactionCollection(repository, options);
        var privateKey = new PrivateKey();
        var tx = new TransactionMetadata
        {
            Signer = privateKey.Address,
        }.Sign(privateKey);
        repository.Nonces.Increase(privateKey.Address, 100);
        Assert.False(transactions.TryAdd(tx));
    }
}
