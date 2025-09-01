// namespace Libplanet.Types;

// public static class TransactionExtensions
// {
//     public static void ValidateTxNonces(
//         this IEnumerable<Transaction> transactions,
//         long blockHeight)
//     {
//         IEnumerable<IGrouping<Address, Transaction>> signerTxs =
//             transactions.OrderBy(tx => tx.Nonce).GroupBy(tx => tx.Signer);
//         BlockHash? genesisHash = null;
//         foreach (IGrouping<Address, Transaction> txs in signerTxs)
//         {
//             long lastNonce = -1L;
//             foreach (Transaction tx in txs)
//             {
//                 long nonce = tx.Nonce;
//                 if (lastNonce >= 0 && lastNonce + 1 != nonce)
//                 {
//                     Address s = tx.Signer;
//                     string msg = nonce <= lastNonce
//                         ? $"The signer {s}'s nonce {nonce} was already consumed before."
//                         : $"The signer {s}'s nonce {lastNonce} has to be added first.";
//                     throw new InvalidOperationException(msg);
//                 }

//                 if (genesisHash is { } g && !tx.GenesisBlockHash.Equals(g))
//                 {
//                     throw new InvalidOperationException(
//                         $"Transactions in the block #{blockHeight} are inconsistent.");
//                 }

//                 lastNonce = nonce;
//                 genesisHash = tx.GenesisBlockHash;
//             }
//         }
//     }
// }
