using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

public abstract record class ConsensusMessage : MessageContent
{
    // protected ConsensusMsg(
    //     Address validator,
    //     int height,
    //     int round)
    // {
    //     Validator = validator;
    //     Round = round;
    //     Height = height;
    // }

    public abstract Address Validator { get; }

    public abstract int Height { get; }

    public abstract int Round { get; }

    // public abstract bool Equals(ConsensusMsg? other);

    // public abstract override bool Equals(object? obj);

    // public abstract override int GetHashCode();
}
