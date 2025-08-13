using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public interface IVoteCollection : IEnumerable<Vote>
{
    int Height { get; }

    int Round { get; }

    ImmutableSortedSet<Validator> Validators { get; }

    Vote this[Address validator] { get; }

    int Count { get; }

    BigInteger TotalVotingPower { get; }

    bool HasTwoThirdsMajority { get; }

    bool HasOneThirdsAny { get; }

    bool HasTwoThirdsAny { get; }

    BlockHash GetMajority23();

    bool TryGetMajority23(out BlockHash blockHash);

    bool TryGetValue(Address validator, [MaybeNullWhen(false)] out Vote value);

    bool Contains(Address validator);

    public ImmutableArray<bool> GetVoteBits(BlockHash blockHash)
    {
        var bitList = new List<bool>(Validators.Count);
        foreach (var validator in Validators)
        {
            if (TryGetValue(validator.Address, out var vote) && vote.BlockHash == blockHash)
            {
                bitList.Add(true);
            }
            else
            {
                bitList.Add(false);
            }
        }

        return [.. bitList];
    }

    public BlockCommit GetBlockCommit()
    {
        var decidedBlockHash = GetMajority23();
        // if (_23Majority is not { } decidedBlockHash)
        // {
        //     throw new InvalidOperationException(
        //         "Cannot create BlockCommit from VoteSet without a two-thirds majority.");
        // }

        var query = from validator in Validators
                    let key = validator.Address
                    let vote = TryGetValue(key, out var vote) ? vote : new VoteMetadata
                    {
                        Height = Height,
                        Round = Round,
                        BlockHash = decidedBlockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = key,
                        ValidatorPower = validator.Power,
                        Type = VoteType.Null,
                    }.WithoutSignature()
                    where vote.BlockHash == decidedBlockHash
                    select vote;

        return new BlockCommit
        {
            Height = Height,
            Round = Round,
            BlockHash = decidedBlockHash,
            Votes = [.. query],
        };
    }
}
