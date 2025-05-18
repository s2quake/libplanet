using Libplanet.Net.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

public abstract class ConsensusMsg : MessageContent, IEquatable<ConsensusMsg>
{
    protected ConsensusMsg(
        Address validator,
        int height,
        int round)
    {
        Validator = validator;
        Round = round;
        Height = height;
    }

    public Address Validator { get; }

    public int Height { get; }

    public int Round { get; }

    public abstract bool Equals(ConsensusMsg? other);

    public abstract override bool Equals(object? obj);

    public abstract override int GetHashCode();
}
