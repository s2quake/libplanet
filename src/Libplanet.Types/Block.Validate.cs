using System.ComponentModel.DataAnnotations;

namespace Libplanet.Types;

public sealed partial record class Block : IValidatableObject
{
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        yield break;
    }

    // internal void ValidateBlock(Block block)
    // {
    //     var height = Tip.Height;
    //     if (block.Height != Tip.Height + 1)
    //     {
    //         throw new InvalidOperationException(
    //             $"The block #{block.Height} {block.BlockHash} is not continuous from the " +
    //             $"topmost block in the chain #{Tip.Height} {Tip.BlockHash}.");
    //     }

    //     int actualProtocolVersion = block.Version;
    //     const int currentProtocolVersion = BlockHeader.CurrentProtocolVersion;

    //     // FIXME: Crude way of checking protocol version for non-genesis block.
    //     // Ideally, whether this is called during instantiation should be made more explicit.
    //     if (actualProtocolVersion > currentProtocolVersion)
    //     {
    //         var message =
    //             $"The protocol version ({actualProtocolVersion}) of the block " +
    //             $"#{block.Height} {block.BlockHash} is not supported by this node." +
    //             $"The highest supported protocol version is {currentProtocolVersion}.";
    //         throw new InvalidOperationException(
    //             message);
    //     }
    //     else if (actualProtocolVersion < Tip.Version)
    //     {
    //         var message =
    //             "The protocol version is disallowed to be downgraded from the topmost block " +
    //             $"in the chain ({actualProtocolVersion} < {Tip.Version}).";
    //         throw new InvalidOperationException(message);
    //     }

    //     Block lastBlock = Blocks[height - 1];
    //     BlockHash? prevHash = lastBlock?.BlockHash;
    //     DateTimeOffset? prevTimestamp = lastBlock?.Timestamp;

    //     if (!block.PreviousHash.Equals(prevHash))
    //     {
    //         throw new InvalidOperationException(
    //             $"The block #{height} {block.BlockHash} is not continuous from the " +
    //             $"block #{height - 1}; while previous block's hash is " +
    //             $"{prevHash}, the block #{height} {block.BlockHash}'s pointer to " +
    //             "the previous hash refers to " +
    //             (block.PreviousHash.ToString() ?? "nothing") + ".");
    //     }

    //     if (block.Timestamp < prevTimestamp)
    //     {
    //         throw new InvalidOperationException(
    //             $"The block #{height} {block.BlockHash}'s timestamp " +
    //             $"({block.Timestamp}) is earlier than " +
    //             $"the block #{height - 1}'s ({prevTimestamp}).");
    //     }

    //     if (block.Height <= 1)
    //     {
    //         if (block.LastCommit != BlockCommit.Empty)
    //         {
    //             throw new InvalidOperationException(
    //                 "The genesis block and the next block should not have lastCommit.");
    //         }
    //     }
    //     else
    //     {
    //         if (block.LastCommit == default)
    //         {
    //             throw new InvalidOperationException(
    //                 "A PBFT block that does not have zero or one index or " +
    //                 "is not a block after a PoW block should have lastCommit.");
    //         }

    //         try
    //         {
    //             var hash = block.PreviousHash == default ? Genesis.BlockHash : block.PreviousHash;
    //             ValidateBlockCommit(Blocks[hash], block.LastCommit);
    //         }
    //         catch (InvalidOperationException ibce)
    //         {
    //             throw new InvalidOperationException(ibce.Message);
    //         }
    //     }

    //     Options.BlockOptions.Validator.Validate(block);

    //     foreach (var tx in block.Transactions)
    //     {
    //         Options.TransactionOptions.Validator.Validate(tx);
    //     }

    //     foreach (var evidence in block.Evidences)
    //     {
    //         var stateRootHash = GetNextStateRootHash(evidence.Height);
    //         var worldState = GetWorld(stateRootHash);
    //         var validators = worldState.GetValidatorSet();
    //         ValidationUtility.Validate(evidence, items: new Dictionary<object, object?>
    //         {
    //             [typeof(EvidenceContext)] = new EvidenceContext(validators),
    //         });
    //     }
    // }
}
