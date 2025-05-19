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

    internal void ValidateBlockCommit(Block block, BlockCommit blockCommit)
    {
        if (block.Height == 0)
        {
            if (blockCommit is { })
            {
                throw new InvalidOperationException(
                    "Genesis block does not have blockCommit.");
            }
            else
            {
                return;
            }
        }

        if (block.Height != 0 && blockCommit == default)
        {
            throw new InvalidOperationException(
                $"Block #{block.BlockHash} BlockCommit is required except for the genesis block.");
        }

        if (block.Height != blockCommit.Height)
        {
            throw new InvalidOperationException(
                "BlockCommit has height value that is not same with block height. " +
                $"Block height is {block.Height}, however, BlockCommit height is " +
                $"{blockCommit.Height}.");
        }

        if (!block.BlockHash.Equals(blockCommit.BlockHash))
        {
            throw new InvalidOperationException(
                $"BlockCommit has different block. Block hash is {block.BlockHash}, " +
                $"however, BlockCommit block hash is {blockCommit.BlockHash}.");
        }

        var validators = GetWorld(block.StateRootHash).GetValidatorSet();
        validators.ValidateBlockCommitValidators(blockCommit);
        BigInteger commitPower = blockCommit.Votes.Aggregate(
            BigInteger.Zero,
            (power, vote) => power + (vote.Flag == VoteFlag.PreCommit
                ? validators.GetValidator(vote.Validator).Power
                : BigInteger.Zero));
        if (validators.GetTwoThirdsPower() >= commitPower)
        {
            throw new InvalidOperationException(
                $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
                $"has insufficient vote power {commitPower} compared to 2/3 of " +
                $"the total power {validators.GetTotalPower()}");
        }
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

    internal void ValidateBlockLoadActions(Block block)
    {
        foreach (var tx in block.Transactions)
        {
            _ = tx.Actions.Select(item => item.ToAction<IAction>());
        }
    }

    internal void ValidateBlock(Block block)
    {
        if (block.Height <= 0)
        {
            throw new ArgumentException(
                $"Given {nameof(block)} must have a positive index but has index {block.Height}",
                nameof(block));
        }

        var height = Blocks.Count;
        if (block.Height != height)
        {
            throw new InvalidOperationException(
                $"The expected index of block {block.BlockHash} is #{height}, " +
                $"but its index is #{block.Height}.");
        }

        int actualProtocolVersion = block.Version;
        const int currentProtocolVersion = BlockHeader.CurrentProtocolVersion;

        // FIXME: Crude way of checking protocol version for non-genesis block.
        // Ideally, whether this is called during instantiation should be made more explicit.
        if (actualProtocolVersion > currentProtocolVersion)
        {
            var message =
                $"The protocol version ({actualProtocolVersion}) of the block " +
                $"#{block.Height} {block.BlockHash} is not supported by this node." +
                $"The highest supported protocol version is {currentProtocolVersion}.";
            throw new InvalidOperationException(
                message);
        }
        else if (actualProtocolVersion < Tip.Version)
        {
            var message =
                "The protocol version is disallowed to be downgraded from the topmost block " +
                $"in the chain ({actualProtocolVersion} < {Tip.Version}).";
            throw new InvalidOperationException(message);
        }

        Block lastBlock = Blocks[height - 1];
        BlockHash? prevHash = lastBlock?.BlockHash;
        DateTimeOffset? prevTimestamp = lastBlock?.Timestamp;

        if (!block.PreviousHash.Equals(prevHash))
        {
            throw new InvalidOperationException(
                $"The block #{height} {block.BlockHash} is not continuous from the " +
                $"block #{height - 1}; while previous block's hash is " +
                $"{prevHash}, the block #{height} {block.BlockHash}'s pointer to " +
                "the previous hash refers to " +
                (block.PreviousHash.ToString() ?? "nothing") + ".");
        }

        if (block.Timestamp < prevTimestamp)
        {
            throw new InvalidOperationException(
                $"The block #{height} {block.BlockHash}'s timestamp " +
                $"({block.Timestamp}) is earlier than " +
                $"the block #{height - 1}'s ({prevTimestamp}).");
        }

        if (block.Height <= 1)
        {
            if (block.LastCommit != BlockCommit.Empty)
            {
                throw new InvalidOperationException(
                    "The genesis block and the next block should not have lastCommit.");
            }
        }
        else
        {
            if (block.LastCommit == default)
            {
                throw new InvalidOperationException(
                    "A PBFT block that does not have zero or one index or " +
                    "is not a block after a PoW block should have lastCommit.");
            }

            try
            {
                var hash = block.PreviousHash == default ? Genesis.BlockHash : block.PreviousHash;
                ValidateBlockCommit(Blocks[hash], block.LastCommit);
            }
            catch (InvalidOperationException ibce)
            {
                throw new InvalidOperationException(ibce.Message);
            }
        }

        foreach (var evidence in block.Evidences)
        {
            var stateRootHash = GetNextStateRootHash(evidence.Height);
            var worldState = GetWorld(stateRootHash ?? default);
            var validators = worldState.GetValidatorSet();
            ValidationUtility.Validate(evidence, items: new Dictionary<object, object?>
            {
                [typeof(EvidenceContext)] = new EvidenceContext(validators),
            });
        }
    }

    internal void ValidateBlockStateRootHash(Block block)
    {
        // NOTE: Since previous hash validation is on block validation,
        // assume block is genesis if previous hash is null.
        if (!(block.PreviousHash is BlockHash previousHash))
        {
            return;
        }

        HashDigest<SHA256> stateRootHash = GetNextStateRootHash(previousHash) ??
            throw new InvalidOperationException(
                $"Cannot validate a block' state root hash as the next " +
                $"state root hash for block {previousHash} is missing.");

        if (!stateRootHash.Equals(block.StateRootHash))
        {
            var message = $"Block #{block.Height} {block.BlockHash}'s state root hash " +
                $"is {block.StateRootHash}, but the execution result is {stateRootHash}.";
            throw new InvalidOperationException(
                message);
        }
    }

    internal void ValidateBlockPrecededStateRootHash(
        Block block, out IReadOnlyList<CommittedActionEvaluation> evaluations)
    {
        var rootHash = DetermineBlockPrecededStateRootHash((RawBlock)block, out evaluations);
        if (!rootHash.Equals(block.StateRootHash))
        {
            var message = $"Block #{block.Height} {block.BlockHash}'s state root hash " +
                $"is {block.StateRootHash}, but the execution result is {rootHash}.";
            throw new InvalidOperationException(
                message);
        }
    }
}
