using Libplanet.Types;

namespace Libplanet.Data;

public sealed class NonceIndex(IDatabase database, int cacheSize = 100)
    : IndexBase<Address, long>(database.GetOrAdd("nonces"), cacheSize)
{
    public long Increase(Address key, long delta = 1L) => this[key] = this.GetValueOrDefault(key) + delta;

    public void Increase(Block block)
    {
        foreach (var transaction in block.Transactions.OrderBy(item => item.Nonce))
        {
            Increase(transaction.Signer, 1L);
        }
    }

    public void Validate(Block block)
    {
        var nonceByAddress = new Dictionary<Address, long>();
        foreach (var transaction in block.Transactions.OrderBy(item => item.Nonce))
        {
            var signer = transaction.Signer;
            var nonce = nonceByAddress.GetValueOrDefault(signer);
            var actualNonce = this.GetValueOrDefault(signer);
            var expectedNonce = nonce + actualNonce;

            if (expectedNonce != transaction.Nonce)
            {
                throw new ArgumentException(
                    $"Transaction {transaction.Id} has an invalid nonce {transaction.Nonce} that is different " +
                    $"from expected nonce {expectedNonce}.",
                    nameof(block));
            }

            nonceByAddress[signer] = nonce + 1L;
        }
    }

    // internal Dictionary<Address, long> ValidateBlockNonces(
    //         Dictionary<Address, long> storedNonces,
    //         Block block)
    // {
    //     var nonceDeltas = new Dictionary<Address, long>();
    //     foreach (Transaction tx in block.Transactions.OrderBy(tx => tx.Nonce))
    //     {
    //         nonceDeltas.TryGetValue(tx.Signer, out var nonceDelta);
    //         storedNonces.TryGetValue(tx.Signer, out var storedNonce);

    //         long expectedNonce = nonceDelta + storedNonce;

    //         if (!expectedNonce.Equals(tx.Nonce))
    //         {
    //             throw new InvalidTxNonceException(
    //                 $"Transaction {tx.Id} has an invalid nonce {tx.Nonce} that is different " +
    //                 $"from expected nonce {expectedNonce}.",
    //                 tx.Id,
    //                 expectedNonce,
    //                 tx.Nonce);
    //         }

    //         nonceDeltas[tx.Signer] = nonceDelta + 1;
    //     }

    //     return nonceDeltas;
    // }

    protected override byte[] ValueToBytes(long value) => BitConverter.GetBytes(value);

    protected override long BytesToValue(byte[] bytes) => BitConverter.ToInt64(bytes, 0);

    protected override string KeyToString(Address key) => key.ToString();

    protected override Address StringToKey(string key) => Address.Parse(key);
}
