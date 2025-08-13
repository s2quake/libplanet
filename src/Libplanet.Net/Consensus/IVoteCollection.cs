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
}
