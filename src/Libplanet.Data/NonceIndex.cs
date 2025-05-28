using Libplanet.Types;
using Libplanet.Types;

namespace Libplanet.Data;

public sealed class NonceIndex(IDatabase database)
    : IndexBase<Address, long>(database.GetOrAdd("nonces"))
{
    public long Increase(Address key, long delta = 1L) => this[key] = this.GetValueOrDefault(key) + delta;

    public void Increase(Block block)
    {
        foreach (var transaction in block.Transactions.OrderBy(item => item.Nonce))
        {
            Increase(transaction.Signer, 1L);
        }
    }

    public void MergeFrom(NonceIndex source)
    {
        foreach (var (key, value) in source)
        {
            if (TryGetValue(key, out var nonce))
            {
                this[key] = Math.Max(nonce, value);
            }
            else
            {
                this[key] = value;
            }
        }
    }

    protected override byte[] GetBytes(long value) => BitConverter.GetBytes(value);

    protected override long GetValue(byte[] bytes) => BitConverter.ToInt64(bytes, 0);
}
