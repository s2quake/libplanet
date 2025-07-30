using Libplanet.Types.Crypto;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public static class TransactionExtensions
{
    public static void ValidateTxNonces(
        this IEnumerable<Transaction> transactions,
        long blockHeight)
    {
        IEnumerable<IGrouping<Address, Transaction>> signerTxs =
            transactions.OrderBy(tx => tx.Nonce).GroupBy(tx => tx.Signer);
        BlockHash? genesisHash = null;
        foreach (IGrouping<Address, Transaction> txs in signerTxs)
        {
            long lastNonce = -1L;
            foreach (Transaction tx in txs)
            {
                long nonce = tx.Nonce;
                if (lastNonce >= 0 && lastNonce + 1 != nonce)
                {
                    Address s = tx.Signer;
                    string msg = nonce <= lastNonce
                        ? $"The signer {s}'s nonce {nonce} was already consumed before."
                        : $"The signer {s}'s nonce {lastNonce} has to be added first.";
                    throw new InvalidOperationException(msg);
                }

                if (genesisHash is { } g && !tx.GenesisHash.Equals(g))
                {
                    throw new InvalidOperationException(
                        $"Transactions in the block #{blockHeight} are inconsistent.");
                }

                lastNonce = nonce;
                genesisHash = tx.GenesisHash;
            }
        }
    }

    // public static UnsignedTx Combine(this TxInvoice invoice, TxSigningMetadata signingMetadata)
    //     => new(invoice, signingMetadata);

    // public static Transaction Sign(this TxInvoice invoice, PrivateKey privateKey, long nonce)
    //     => invoice.Combine(new TxSigningMetadata(privateKey.Address, nonce)).Sign(privateKey);

    // public static Transaction Sign(this UnsignedTx unsignedTx, PrivateKey privateKey)
    //     => new(unsignedTx, privateKey);

    // public static Transaction Verify(this UnsignedTx unsignedTx, ImmutableArray<byte> signature)
    //     => new(unsignedTx, signature);

    // internal static Transaction CombineWithoutVerification(
    //     this UnsignedTx unsignedTx,
    //     ImmutableArray<byte> alreadyVerifiedSignature) =>
    //     Transaction.CombineWithoutVerification(unsignedTx, alreadyVerifiedSignature);
}
