using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteCollection(int height, int round, VoteType voteType, ImmutableSortedSet<Validator> validators)
    : IEnumerable<Vote>
{
    private readonly Dictionary<Address, Vote> _voteByValidator = [];
    private readonly Dictionary<BlockHash, ImmutableArray<Vote>> _votesByBlockHash = [];
    private BlockHash _blockHash;

    public Vote this[int index]
    {
        get
        {
            var validator = validators[index];
            if (_voteByValidator.TryGetValue(validator.Address, out var vote))
            {
                return vote;
            }

            throw new KeyNotFoundException($"No vote found for validator {validator.Address} at index {index}.");
        }
    }

    public Vote this[Address validator] => _voteByValidator[validator];

    public BigInteger TotalVotingPower => validators.GetValidatorsPower([.. _voteByValidator.Values.Select(vote => vote.Validator)]);

    public BlockHash BlockHash => _blockHash;

    public bool HasTwoThirdsMajority => _blockHash != default;

    public bool HasOneThirdsAny => TotalVotingPower > validators.GetOneThirdPower();

    public bool HasTwoThirdsAny => TotalVotingPower > validators.GetTwoThirdsPower();

    public void Add(Vote vote)
    {
        if (vote.Height != height)
        {
            throw new ArgumentException(
                $"Vote height {vote.Height} does not match expected height {height}", nameof(vote));
        }

        if (vote.Round != round)
        {
            throw new ArgumentException(
                $"Vote round {vote.Round} does not match expected round {round}", nameof(vote));
        }

        if (vote.Type != voteType)
        {
            throw new ArgumentException(
                $"Vote type {vote.Type} does not match expected type {voteType}", nameof(vote));
        }

        if (!validators.Contains(vote.Validator))
        {
            throw new ArgumentException(
                $"Validator {vote.Validator} is not in the validators for height {height}", nameof(vote));
        }

        if (validators.GetValidator(vote.Validator).Power != vote.ValidatorPower)
        {
            throw new ArgumentException(
                $"Validator {vote.Validator} power {vote.ValidatorPower} does not match " +
                $"expected power {validators.GetValidator(vote.Validator).Power}", nameof(vote));
        }

        var validator = vote.Validator;
        var blockHash = vote.BlockHash;

        if (_voteByValidator.TryGetValue(validator, out var oldVote))
        {
            if (oldVote.BlockHash.Equals(vote.BlockHash))
            {
                throw new ArgumentException($"{nameof(Add)}() does not expect duplicate votes", nameof(vote));
            }
            else if (_blockHash == vote.BlockHash)
            {
                _voteByValidator[validator] = vote;
            }
            else
            {
                throw new DuplicateVoteException("There's a conflicting vote", oldVote, vote);
            }
        }
        else
        {
            _voteByValidator.Add(validator, vote);
        }

        if (!_votesByBlockHash.TryGetValue(blockHash, out var votes))
        {
            _votesByBlockHash[blockHash] = [vote];
            votes = _votesByBlockHash[blockHash];
        }
        else
        {
            _votesByBlockHash[blockHash] = votes.Add(vote);
            votes = _votesByBlockHash[blockHash];
        }

        var totalPower = BigIntegerUtility.Sum(votes.Select(v => v.ValidatorPower));
        var quorum = validators.GetTwoThirdsPower() + 1;

        if (quorum <= totalPower && _blockHash == default)
        {
            _blockHash = vote.BlockHash;
        }
    }

    public bool TryGetValue(Address validator, [MaybeNullWhen(false)] out Vote value)
        => _voteByValidator.TryGetValue(validator, out value);

    public bool TryGetValue(int index, [MaybeNullWhen(false)] out Vote value)
    {
        if (index < 0 || index >= validators.Count)
        {
            value = null;
            return false;
        }

        var validator = validators[index];
        return _voteByValidator.TryGetValue(validator.Address, out value);
    }

    public bool Contains(Address validator) => _voteByValidator.ContainsKey(validator);

    public bool Contains(int index)
    {
        if (index < 0 || index >= validators.Count)
        {
            return false;
        }

        var validator = validators[index];
        return _voteByValidator.ContainsKey(validator.Address);
    }

    public ImmutableArray<bool> GetVoteBits(BlockHash blockHash)
    {
        var bitList = new List<bool>(validators.Count);
        foreach (var validator in validators)
        {
            if (_voteByValidator.TryGetValue(validator.Address, out var vote) && vote.BlockHash == blockHash)
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
        if (_blockHash == default)
        {
            throw new InvalidOperationException(
                "Cannot create BlockCommit from VoteSet without a two-thirds majority.");
        }

        var query = from validator in validators
                    let key = validator.Address
                    let vote = _voteByValidator.TryGetValue(key, out var vote) ? vote : new VoteMetadata
                    {
                        Height = height,
                        Round = round,
                        BlockHash = _blockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = key,
                        ValidatorPower = validator.Power,
                        Type = VoteType.Null,
                    }.WithoutSignature()
                    where vote.BlockHash == _blockHash
                    select vote;

        return new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = _blockHash,
            Votes = [.. query],
        };
    }

    public IEnumerator<Vote> GetEnumerator()
    {
        foreach (var validator in validators)
        {
            if (_voteByValidator.TryGetValue(validator.Address, out var vote))
            {
                yield return vote;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
