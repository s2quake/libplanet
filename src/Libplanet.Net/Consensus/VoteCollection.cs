using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteCollection : IReadOnlyDictionary<Address, Vote>
{
    private readonly ConcurrentDictionary<Address, Vote> _voteByAddress = new();

    public Vote this[Address key]
    {
        get => _voteByAddress[key];
        set
        {
            _voteByAddress.AddOrUpdate(key, value, (k, oldVote) =>
            {
                TotalPower -= oldVote.ValidatorPower;
                return value;
            });

            TotalPower += value.ValidatorPower;
        }
    }

    public BigInteger TotalPower { get; private set; }

    public IEnumerable<Address> Keys => _voteByAddress.Keys;

    public IEnumerable<Vote> Values => _voteByAddress.Values;

    public int Count => _voteByAddress.Count;

    public ImmutableArray<Vote> ResolveVotes(
        int height, int round, BlockHash blockHash, ImmutableSortedSet<Validator> validators)
    {
        var query = from validator in validators
                    let key = validator.Address
                    select TryGetValue(key, out var vote) ? vote : new VoteMetadata
                    {
                        Height = height,
                        Round = round,
                        BlockHash = blockHash,
                        Timestamp = DateTimeOffset.UtcNow,
                        Validator = key,
                        ValidatorPower = validator.Power,
                        Flag = VoteFlag.Null,
                    }.WithoutSignature();
        return [.. query];
    }

    public bool ContainsKey(Address key) => _voteByAddress.ContainsKey(key);

    public IEnumerator<KeyValuePair<Address, Vote>> GetEnumerator() => _voteByAddress.GetEnumerator();

    public bool TryGetValue(Address key, [MaybeNullWhen(false)] out Vote value)
         => _voteByAddress.TryGetValue(key, out value);

    IEnumerator IEnumerable.GetEnumerator() => _voteByAddress.GetEnumerator();
}
