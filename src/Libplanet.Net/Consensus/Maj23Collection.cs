using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Maj23Collection : IMaj23Collection
{
    private readonly int _height;
    private readonly int _round;
    private readonly VoteType _voteType;
    private readonly ImmutableSortedSet<Validator> _validators;
    private readonly Dictionary<Address, Maj23> _maj23ByValidator = [];
    private readonly HashSet<BlockHash> _maj23BlockHashes = [];

    public Maj23Collection(int height, int round, VoteType voteType, ImmutableSortedSet<Validator> validators)
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

    public Maj23 this[Address validator] => _maj23ByValidator[validator];

    public int Count => _maj23ByValidator.Count;

    public void Add(Maj23 maj23)
    {
        if (maj23.Height != _height)
        {
            throw new ArgumentException(
                $"Vote height {maj23.Height} does not match expected height {_height}", nameof(maj23));
        }

        if (maj23.Round != _round)
        {
            throw new ArgumentException(
                $"Vote round {maj23.Round} does not match expected round {_round}", nameof(maj23));
        }

        if (maj23.VoteType != _voteType)
        {
            throw new ArgumentException(
                $"Vote type {maj23.VoteType} does not match expected type {_voteType}", nameof(maj23));
        }

        if (!_validators.Contains(maj23.Validator))
        {
            throw new ArgumentException(
                $"Validator {maj23.Validator} is not in the validators for height {_height}", nameof(maj23));
        }

        _maj23ByValidator.Add(maj23.Validator, maj23);
        _maj23BlockHashes.Add(maj23.BlockHash);
    }

    public bool HasMaj23(BlockHash blockHash)
        => _maj23BlockHashes.Contains(blockHash);

    public bool TryGetValue(Address validator, [MaybeNullWhen(false)] out Maj23 value)
        => _maj23ByValidator.TryGetValue(validator, out value);

    public bool Contains(Address validator) => _maj23ByValidator.ContainsKey(validator);

    public IEnumerator<Maj23> GetEnumerator()
    {
        foreach (var validator in _validators)
        {
            if (_maj23ByValidator.TryGetValue(validator.Address, out var maj23))
            {
                yield return maj23;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
