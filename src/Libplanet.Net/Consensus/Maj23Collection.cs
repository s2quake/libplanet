using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Maj23Collection(int height, int round, VoteType voteType, ImmutableSortedSet<Validator> validators)
    : IEnumerable<Maj23>
{
    private readonly Dictionary<Address, Maj23> _maj23ByValidator = [];

    public Maj23 this[Address validator] => _maj23ByValidator[validator];

    public void Add(Maj23 maj23)
    {
        if (maj23.Height != height)
        {
            throw new ArgumentException(
                $"Vote height {maj23.Height} does not match expected height {height}", nameof(maj23));
        }

        if (maj23.Round != round)
        {
            throw new ArgumentException(
                $"Vote round {maj23.Round} does not match expected round {round}", nameof(maj23));
        }

        if (maj23.VoteType != voteType)
        {
            throw new ArgumentException(
                $"Vote type {maj23.VoteType} does not match expected type {voteType}", nameof(maj23));
        }

        if (!validators.Contains(maj23.Validator))
        {
            throw new ArgumentException(
                $"Validator {maj23.Validator} is not in the validators for height {height}", nameof(maj23));
        }

        _maj23ByValidator.Add(maj23.Validator, maj23);
    }

    public bool TryGetValue(Address validator, [MaybeNullWhen(false)] out Maj23 value)
        => _maj23ByValidator.TryGetValue(validator, out value);

    public bool ContainsKey(Address validator) => _maj23ByValidator.ContainsKey(validator);

    public IEnumerator<Maj23> GetEnumerator()
    {
        foreach (var validator in validators)
        {
            if (_maj23ByValidator.TryGetValue(validator.Address, out var maj23))
            {
                yield return maj23;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
