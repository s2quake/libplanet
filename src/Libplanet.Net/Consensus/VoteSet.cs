using System.Collections.Concurrent;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public class VoteSet(
    int height, int round, VoteFlag voteFlag, ImmutableSortedSet<Validator> validators)
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<Address, Vote> _votes = [];
    private readonly Dictionary<BlockHash, BlockVotes> _votesByBlock = [];
    private readonly Dictionary<Address, BlockHash> _peerMaj23s = [];
    private BlockHash? _maj23;

    public ImmutableSortedSet<Validator> Validators { get; } = validators;

    public BigInteger Sum => Validators.GetValidatorsPower([.. _votes.Values.Select(vote => vote.Validator)]);

    public int Count => _votes.Count;

    public int TotalCount => _votesByBlock.Values.Sum(votes => votes.Votes.Count);

    public bool Contains(Address address, BlockHash blockHash)
    {
        return _votes.Values.Any(
            vote => vote.Validator.Equals(address)
            && vote.BlockHash.Equals(blockHash));
    }

    public Vote? GetVote(Address address, BlockHash blockHash)
    {
        Vote vote;
        try
        {
            vote = _votes[address];
            if (vote.BlockHash.Equals(blockHash))
            {
                return vote;
            }
        }
        catch (KeyNotFoundException)
        {
        }

        try
        {
            return _votesByBlock[blockHash].Votes[address];
        }
        catch (KeyNotFoundException)
        {
        }

        return null;
    }

    public IEnumerable<Vote> GetVotes(BlockHash blockHash) => _votesByBlock[blockHash].Votes.Values;

    public IEnumerable<Vote> GetAllVotes()
    {
        var list = new List<Vote>();
        foreach (var votes in _votesByBlock.Values)
        {
            list.AddRange(votes.Votes.Values);
        }

        return list;
    }

    public bool SetPeerMaj23(Maj23 maj23)
    {
        lock (_lock)
        {
            var validator = maj23.Validator;
            var blockHash = maj23.BlockHash;

            if (_peerMaj23s.TryGetValue(validator, out BlockHash hash))
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

            _peerMaj23s[validator] = blockHash;

            if (!_votesByBlock.TryGetValue(blockHash, out BlockVotes? value))
            {
                value = new BlockVotes(blockHash);
                _votesByBlock[blockHash] = value;
            }

            BlockVotes votesByBlock = value;
            if (votesByBlock.PeerMaj23)
            {
                return false;
            }
            else
            {
                votesByBlock.PeerMaj23 = true;
                return true;
            }
        }
    }

    public bool[] BitArray()
    {
        lock (_lock)
        {
            return [.. Validators.Select(validator => _votes.ContainsKey(validator.Address))];
        }
    }

    public bool[] BitArrayByBlockHash(BlockHash blockHash)
    {
        lock (_lock)
        {
            if (_votesByBlock.ContainsKey(blockHash))
            {
                return Validators.Select(validator =>
                    _votesByBlock[blockHash].Votes.ContainsKey(validator.Address)).ToArray();
            }

            return [.. Validators.Select(_ => false)];
        }
    }

    public List<Vote> List() => [.. _votes.Values.OrderBy(vote => vote.Validator)];

    public List<Vote> MappedList()
    {
        if (_maj23 is { } maj23NotNull)
        {
            return _votesByBlock[maj23NotNull].MappedList(height, round, Validators);
        }

        throw new NullReferenceException();
    }

    public bool HasTwoThirdsMajority()
    {
        lock (_lock)
        {
            return _maj23 is not null;
        }
    }

    public bool IsCommit()
    {
        if (voteFlag != VoteFlag.PreCommit)
        {
            return false;
        }

        return HasTwoThirdsMajority();
    }

    public bool HasOneThirdsAny()
    {
        lock (_lock)
        {
            return Sum > Validators.GetOneThirdPower();
        }
    }

    public bool HasTwoThirdsAny()
    {
        lock (_lock)
        {
            return Sum > Validators.GetTwoThirdsPower();
        }
    }

    public bool HasAll()
    {
        lock (_lock)
        {
            return Sum == Validators.GetTotalPower();
        }
    }

    public bool TwoThirdsMajority(out BlockHash blockHash)
    {
        lock (_lock)
        {
            if (_maj23 is { } maj23)
            {
                blockHash = maj23;
                return true;
            }

            blockHash = default;
            return false;
        }
    }

    public BlockCommit ToBlockCommit()
    {
        if (!IsCommit())
        {
            return BlockCommit.Empty;
        }

        throw new NotImplementedException();
        // return new BlockCommit
        // {
        //     Height = _height,
        //     Round = _round,
        //     BlockHash = _maj23!.Value,
        //     Votes = MappedList().ToImmutableArray(),
        // };
    }

    internal void AddVote(Vote vote)
    {
        if (vote.Round != round || vote.Flag != voteFlag)
        {
            throw new InvalidVoteException("Round, flag of the vote mismatches", vote);
        }

        var validator = vote.Validator;
        var blockHash = vote.BlockHash;

        // Already exists in voteSet.votes?
        if (_votes.TryGetValue(validator, out var oldVote))
        {
            if (oldVote.BlockHash.Equals(vote.BlockHash))
            {
                throw new InvalidVoteException($"{nameof(AddVote)}() does not expect duplicate votes", vote);
            }

            // Replace vote if blockKey matches voteSet.maj23.
            if (Equals(_maj23, blockHash))
            {
                _votes[validator] = vote;
            }

            // Otherwise don't add it to voteSet.votes
        }
        else
        {
            _votes[validator] = vote;
        }

        if (_votesByBlock.ContainsKey(blockHash))
        {
            if (oldVote is not null && !_votesByBlock[blockHash].PeerMaj23)
            {
                // There's a conflict and no peer claims that this block is special.
                throw new InvalidVoteException(
                    "There's a conflict and no peer claims that this block is special",
                    vote);
            }

            // We'll add the vote in a bit.
        }
        else
        {
            // .votesByBlock doesn't exist...
            if (oldVote is not null)
            {
                // ... and there's a conflicting vote.
                // We're not even tracking this blockKey, so just forget it.
                throw new DuplicateVoteException(
                    message: "There's a conflicting vote",
                    voteRef: oldVote,
                    voteDup: vote);
            }

            // ... and there's no conflicting vote.
            // Start tracking this blockKey
            _votesByBlock[blockHash] = new BlockVotes(blockHash);

            // We'll add the vote in a bit.
        }

        BlockVotes votesByBlock = _votesByBlock[blockHash];

        // Before adding to votesByBlock, see if we'll exceed quorum
        BigInteger origSum = votesByBlock.Sum;
        BigInteger quorum = Validators.GetTwoThirdsPower() + 1;

        // Add vote to votesByBlock
        votesByBlock.AddVerifiedVote(vote, Validators.GetValidator(validator).Power);

        // If we just crossed the quorum threshold and have 2/3 majority...
        if (origSum < quorum && quorum <= votesByBlock.Sum && _maj23 is null)
        {
            _maj23 = vote.BlockHash;

            // And also copy votes over to voteSet.votes
            foreach (var pair in votesByBlock.Votes)
            {
                _votes[pair.Key] = pair.Value;
            }
        }
    }

    internal sealed class BlockVotes(BlockHash blockHash)
    {
        public BlockHash BlockHash { get; } = blockHash;

        public bool PeerMaj23 { get; set; }

        public Dictionary<Address, Vote> Votes { get; } = [];

        public BigInteger Sum { get; set; } = BigInteger.Zero;

        public void AddVerifiedVote(Vote vote, BigInteger power)
        {
            if (Votes.ContainsKey(vote.Validator))
            {
                return;
            }

            Votes[vote.Validator] = vote;
            Sum += power;
        }

        public List<Vote> MappedList(int height, int round, ImmutableSortedSet<Validator> validatorSet)
            => validatorSet.Select(item => item.Address).Select(
                key => Votes.TryGetValue(key, out Vote? value) ? value : new VoteMetadata
                {
                    Height = height,
                    Round = round,
                    BlockHash = BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = key,
                    ValidatorPower = validatorSet.GetValidator(key).Power,
                    Flag = VoteFlag.Null,
                }.Sign(null!))
                .ToList();
    }
}
