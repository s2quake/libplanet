using Libplanet.Store;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies;

public sealed record class StagedTransaction
{
    public required Transaction Transaction { get; init; }

    public required DateTimeOffset Lifetime { get; init; }

    public bool IsIgnored { get; init; }

    public bool IsExpired => Lifetime < DateTimeOffset.UtcNow;

    public bool IsEnabled(IStore store, Guid blockChainId)
    {
        if (Lifetime > DateTimeOffset.UtcNow)
        {
            return false;
        }

        if (store.GetTxNonce(blockChainId, Transaction.Signer) < Transaction.Nonce)
        {
            return false;
        }

        return !IsIgnored;
    }
}
