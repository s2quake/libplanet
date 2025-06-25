using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteCollection(int height, int round, VoteType voteType, ImmutableSortedSet<Validator> validators)
    : IReadOnlyDictionary<Address, Vote>
{
    private readonly Dictionary<Address, Vote> _voteByValidator = [];
    private readonly Dictionary<Address, BlockHash> _maj23ByValidator = [];
    private BlockHash? _maj23;

    public BigInteger Sum => validators.GetValidatorsPower([.. _voteByValidator.Values.Select(vote => vote.Validator)]);

    public int Count => _voteByValidator.Count;

    public bool HasTwoThirdsMajority => _maj23 is not null;

    public bool HasOneThirdsAny => Sum > validators.GetOneThirdPower();

    public bool HasTwoThirdsAny => Sum > validators.GetTwoThirdsPower();

    public IEnumerable<Address> Keys => _voteByValidator.Keys;

    public IEnumerable<Vote> Values => _voteByValidator.Values;

    public Vote this[Address key] => _voteByValidator[key];

    public IEnumerable<Vote> GetAllVotes() => _voteByValidator.Values;

    public bool SetMaj23(Maj23 maj23)
    {
        var validator = maj23.Validator;
        var blockHash = maj23.BlockHash;

        if (_maj23ByValidator.TryGetValue(validator, out var hash))
        {
            if (hash.Equals(blockHash))
            {
                return false;
            }

            throw new InvalidMaj23Exception(
                $"Received conflicting BlockHash from peer {validator} " +
                $"(Expected: {hash}, Actual: {blockHash})",
                maj23);
        }

        _maj23ByValidator[validator] = blockHash;
        return true;
    }

    public bool[] BitArrayByBlockHash(BlockHash blockHash)
    {
        var itemList = new List<bool>(validators.Count);
        foreach (var vote in _voteByValidator.Values.Where(item => item.BlockHash == blockHash))
        {
            itemList.Add(validators.Contains(vote.Validator));
        }
        return [.. itemList];
    }

    public ImmutableArray<Vote> MappedList()
    {
        if (_maj23 is { } maj23NotNull)
        {
            var query = from validator in validators
                        let key = validator.Address
                        let vote = _voteByValidator.TryGetValue(key, out var vote) ? vote : new VoteMetadata
                        {
                            Height = height,
                            Round = round,
                            BlockHash = maj23NotNull,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = key,
                            ValidatorPower = validator.Power,
                            Type = VoteType.Null,
                        }.WithoutSignature()
                        where vote.BlockHash == maj23NotNull
                        select vote;
            return [.. query];
        }

        throw new InvalidOperationException(
            "Cannot create BlockCommit from VoteSet without a two-thirds majority.");
    }

    public bool TwoThirdsMajority(out BlockHash blockHash)
    {
        if (_maj23 is { } maj23)
        {
            blockHash = maj23;
            return true;
        }

        blockHash = default;
        return false;
    }

    public BlockCommit ToBlockCommit()
    {
        if (voteType != VoteType.PreCommit || _maj23 is null)
        {
            return BlockCommit.Empty;
        }

        return new BlockCommit
        {
            Height = height,
            Round = round,
            BlockHash = _maj23.Value,
            Votes = MappedList(),
        };
    }

    public void Add(Vote vote)
    {
        if (vote.Round != round || vote.Type != voteType)
        {
            var message = $"Vote round {vote.Round} or type {vote.Type} does not match " +
                          $"VoteCollection round {round} or type {voteType}";
            throw new ArgumentException(message, nameof(vote));
        }

        var validator = vote.Validator;
        var blockHash = vote.BlockHash;

        if (_voteByValidator.TryGetValue(validator, out var oldVote))
        {
            if (oldVote.BlockHash.Equals(vote.BlockHash))
            {
                throw new ArgumentException($"{nameof(Add)}() does not expect duplicate votes", nameof(vote));
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

        var votes = _voteByValidator.Values.Where(item => item.BlockHash == blockHash).ToArray();
        var totalPower2 = votes.Aggregate(BigInteger.Zero, (n, i) => n + i.ValidatorPower);
        var totalPower1 = totalPower2 - vote.ValidatorPower;
        var quorum = validators.GetTwoThirdsPower() + 1;
        if (totalPower1 < quorum && quorum <= totalPower2 && _maj23 is null)
        {
            _maj23 = vote.BlockHash;
        }
    }

    public bool ContainsKey(Address key) => _voteByValidator.ContainsKey(key);

    public bool TryGetValue(Address key, [MaybeNullWhen(false)] out Vote value)
        => _voteByValidator.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Address, Vote>> GetEnumerator() => _voteByValidator.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
