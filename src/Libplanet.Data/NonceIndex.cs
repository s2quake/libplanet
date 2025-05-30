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

    protected override byte[] ValueToBytes(long value) => BitConverter.GetBytes(value);

    protected override long BytesToValue(byte[] bytes) => BitConverter.ToInt64(bytes, 0);

    protected override string KeyToString(Address key) => key.ToString();

    protected override Address StringToKey(string key) => Address.Parse(key);
}
