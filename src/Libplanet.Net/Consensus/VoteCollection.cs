using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteCollection : IEnumerable<Vote>
{
    private readonly int _height;
    private readonly int _round;
    private readonly VoteType _voteType;
    private readonly ImmutableSortedSet<Validator> _validators;
    private readonly Dictionary<Address, Vote> _voteByValidator = [];
    private readonly Dictionary<BlockHash, ImmutableArray<Vote>> _votesByBlockHash = [];
    private BlockHash? _23Majority;

    public VoteCollection(int height, int round, VoteType voteType, ImmutableSortedSet<Validator> validators)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 0);
        if (voteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentException(
                $"Invalid vote type: {voteType}. Expected PreVote or PreCommit.", nameof(voteType));
        }

        if (validators.Count is 0)
        {
            throw new ArgumentException("Validators cannot be empty.", nameof(validators));
        }

        _height = height;
        _round = round;
        _voteType = voteType;
        _validators = validators;
    }

    public Vote this[Address validator] => _voteByValidator[validator];

    public int Count => _voteByValidator.Count;

    public BigInteger TotalVotingPower => _validators.GetValidatorsPower([.. _voteByValidator.Values.Select(vote => vote.Validator)]);

    public bool HasTwoThirdsMajority => _23Majority is not null;

    public bool HasOneThirdsAny => TotalVotingPower > _validators.GetOneThirdPower();

    public bool HasTwoThirdsAny => TotalVotingPower > _validators.GetTwoThirdsPower();

    public void Add(Vote vote)
    {
        if (vote.Height != _height)
        {
            throw new ArgumentException(
                $"Vote height {vote.Height} does not match expected height {_height}", nameof(vote));
        }

        if (vote.Round != _round)
        {
            throw new ArgumentException(
                $"Vote round {vote.Round} does not match expected round {_round}", nameof(vote));
        }

        if (vote.Type != _voteType)
        {
            throw new ArgumentException(
                $"Vote type {vote.Type} does not match expected type {_voteType}", nameof(vote));
        }

        if (!_validators.Contains(vote.Validator))
        {
            throw new ArgumentException(
                $"Validator {vote.Validator} is not in the validators for height {_height}", nameof(vote));
        }

        if (_validators.GetValidator(vote.Validator).Power != vote.ValidatorPower)
        {
            throw new ArgumentException(
                $"Validator {vote.Validator} power {vote.ValidatorPower} does not match " +
                $"expected power {_validators.GetValidator(vote.Validator).Power}", nameof(vote));
        }

        var validator = vote.Validator;
        var blockHash = vote.BlockHash;

        if (_voteByValidator.TryGetValue(validator, out var oldVote))
        {
            if (oldVote.BlockHash.Equals(vote.BlockHash))
            {
                throw new ArgumentException($"{nameof(Add)}() does not expect duplicate votes", nameof(vote));
            }
            else if (_23Majority == vote.BlockHash)
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
        var quorum = _validators.GetTwoThirdsPower() + 1;

        if (quorum <= totalPower && _23Majority is null)
        {
            _23Majority = vote.BlockHash;
        }
    }

    public BlockHash GetMajority23()
        => _23Majority ?? throw new InvalidOperationException(
            "No consensus has been reached yet. Check HasTwoThirdsMajority first.");

    public bool TryGetMajority23(out BlockHash blockHash)
    {
        if (_23Majority is { } hash)
        {
            blockHash = hash;
            return true;
        }

        blockHash = default;
        return false;
    }

    public bool TryGetValue(Address validator, [MaybeNullWhen(false)] out Vote value)
        => _voteByValidator.TryGetValue(validator, out value);

    public bool Contains(Address validator) => _voteByValidator.ContainsKey(validator);

    public bool Contains(int index)
    {
        if (index < 0 || index >= _validators.Count)
        {
            return false;
        }

        var validator = _validators[index];
        return _voteByValidator.ContainsKey(validator.Address);
    }

    public ImmutableArray<bool> GetVoteBits(BlockHash blockHash)
    {
        var bitList = new List<bool>(_validators.Count);
        foreach (var validator in _validators)
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
        if (_23Majority is not { } decidedBlockHash)
        {
            throw new InvalidOperationException(
                "Cannot create BlockCommit from VoteSet without a two-thirds majority.");
        }

        var query = from validator in _validators
                    let key = validator.Address
                    let vote = _voteByValidator.TryGetValue(key, out var vote) ? vote : new VoteMetadata
                    {
                        Height = _height,
                        Round = _round,
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
            Height = _height,
            Round = _round,
            BlockHash = decidedBlockHash,
            Votes = [.. query],
        };
    }

    public IEnumerator<Vote> GetEnumerator()
    {
        foreach (var validator in _validators)
        {
            if (_voteByValidator.TryGetValue(validator.Address, out var vote))
            {
                yield return vote;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
