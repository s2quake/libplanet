using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteCollection : IVoteCollection
{
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

        Height = height;
        Round = round;
        Type = voteType;
        Validators = validators;
    }

    public Vote this[Address validator] => _voteByValidator[validator];

    public int Height { get; }

    public int Round { get; }

    public VoteType Type { get; }

    public ImmutableSortedSet<Validator> Validators { get; }

    public int Count => _voteByValidator.Count;

    public BigInteger TotalVotingPower => Validators.GetValidatorsPower([.. _voteByValidator.Values.Select(vote => vote.Validator)]);

    public bool HasTwoThirdsMajority => _23Majority is not null;

    public bool HasOneThirdsAny => TotalVotingPower > Validators.GetOneThirdPower();

    public bool HasTwoThirdsAny => TotalVotingPower > Validators.GetTwoThirdsPower();

    public void Add(Vote vote)
    {
        if (vote.Height != Height)
        {
            throw new ArgumentException(
                $"Vote height {vote.Height} does not match expected height {Height}", nameof(vote));
        }

        if (vote.Round != Round)
        {
            throw new ArgumentException(
                $"Vote round {vote.Round} does not match expected round {Round}", nameof(vote));
        }

        if (vote.Type != Type)
        {
            throw new ArgumentException(
                $"Vote type {vote.Type} does not match expected type {Type}", nameof(vote));
        }

        if (!Validators.Contains(vote.Validator))
        {
            throw new ArgumentException(
                $"Validator {vote.Validator} is not in the validators for height {Height}", nameof(vote));
        }

        if (Validators.GetValidator(vote.Validator).Power != vote.ValidatorPower)
        {
            throw new ArgumentException(
                $"Validator {vote.Validator} power {vote.ValidatorPower} does not match " +
                $"expected power {Validators.GetValidator(vote.Validator).Power}", nameof(vote));
        }

        var validator = vote.Validator;
        var blockHash = vote.BlockHash;

        if (!_voteByValidator.TryAdd(validator, vote))
        {
            throw new ArgumentException("Duplicate vote detected.", nameof(vote));
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
        var quorum = Validators.GetTwoThirdsPower() + 1;

        if (quorum <= totalPower && _23Majority is null)
        {
            _23Majority = vote.BlockHash;
        }
    }

    public bool Remove(Address address)
    {
        if (!_voteByValidator.TryGetValue(address, out var vote))
        {
            return false;
        }

        _voteByValidator.Remove(address);
        if (!_votesByBlockHash.TryGetValue(vote.BlockHash, out var votes))
        {
            throw new UnreachableException("Votes for block hash not found");
        }

        votes = votes.Remove(vote);
        if (votes.IsEmpty)
        {
            _votesByBlockHash.Remove(vote.BlockHash);
        }
        else
        {
            _votesByBlockHash[vote.BlockHash] = votes;
        }

        var totalPower = BigIntegerUtility.Sum(votes.Select(v => v.ValidatorPower));
        var quorum = Validators.GetTwoThirdsPower() + 1;

        if (quorum > totalPower && _23Majority is not null)
        {
            _23Majority = null;
        }

        return true;
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
        if (index < 0 || index >= Validators.Count)
        {
            return false;
        }

        var validator = Validators[index];
        return _voteByValidator.ContainsKey(validator.Address);
    }

    public ImmutableArray<bool> GetVoteBits(BlockHash blockHash)
    {
        var bitList = new List<bool>(Validators.Count);
        foreach (var validator in Validators)
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

        var query = from validator in Validators
                    let key = validator.Address
                    let vote = _voteByValidator.TryGetValue(key, out var vote) ? vote : new VoteMetadata
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

    public IEnumerator<Vote> GetEnumerator()
    {
        foreach (var validator in Validators)
        {
            if (_voteByValidator.TryGetValue(validator.Address, out var vote))
            {
                yield return vote;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
