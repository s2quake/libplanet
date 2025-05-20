using Libplanet.Blockchain;
using Libplanet.Store;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Tests.Blockchain;

public sealed class StagedTransactionCollectionTest
{
    [Fact]
    public void AddTransaction()
    {
        var options = new BlockChainOptions
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
        var options = new BlockChainOptions
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
        repository.Chain.Nonces.Increase(privateKey.Address, 100);
        Assert.False(transactions.TryAdd(tx));
    }
}
