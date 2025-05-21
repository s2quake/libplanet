using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    internal static Dictionary<Address, long> ValidateGenesisNonces(Block block)
    {
        var nonceDeltas = new Dictionary<Address, long>();
        foreach (var tx in block.Transactions.OrderBy(tx => tx.Nonce))
        {
            nonceDeltas.TryGetValue(tx.Signer, out var nonceDelta);
            long expectedNonce = nonceDelta;

            if (!expectedNonce.Equals(tx.Nonce))
            {
                throw new InvalidOperationException(
                    $"Transaction {tx.Id} has an invalid nonce {tx.Nonce} that is different " +
                    $"from expected nonce {expectedNonce}.");
            }

            nonceDeltas[tx.Signer] = nonceDelta + 1;
        }

        return nonceDeltas;
    }

    

    internal Dictionary<Address, long> ValidateBlockNonces(
        Dictionary<Address, long> storedNonces,
        Block block)
    {
        var nonceDeltas = new Dictionary<Address, long>();
        foreach (Transaction tx in block.Transactions.OrderBy(tx => tx.Nonce))
        {
            nonceDeltas.TryGetValue(tx.Signer, out var nonceDelta);
            storedNonces.TryGetValue(tx.Signer, out var storedNonce);

            long expectedNonce = nonceDelta + storedNonce;

            if (!expectedNonce.Equals(tx.Nonce))
            {
                throw new InvalidOperationException(
                    $"Transaction {tx.Id} has an invalid nonce {tx.Nonce} that is different " +
                    $"from expected nonce {expectedNonce}.");
            }

            nonceDeltas[tx.Signer] = nonceDelta + 1;
        }

        return nonceDeltas;
    }
}
