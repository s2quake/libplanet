using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class ConsensusMaj23Message : ConsensusMessage
{
    // public ConsensusMaj23Msg(Maj23 maj23)
    //     : base(maj23.Validator, maj23.Height, maj23.Round)
    // {
    //     Maj23 = maj23;
    // }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsensusMaj23Msg"/> class
    /// with marshalled message.
    /// </summary>
    /// <param name="dataframes">A marshalled message.</param>
    // public ConsensusMaj23Msg(byte[][] dataframes)
    //     : this(maj23: ModelSerializer.DeserializeFromBytes<Maj23>(dataframes[0]))
    // {
    // }

    [Property(0)]
    public required Maj23 Maj23 { get; init; }

    /// <inheritdoc cref="MessageContent.DataFrames"/>
    // public override IEnumerable<byte[]> DataFrames =>
    //     new List<byte[]> { ModelSerializer.SerializeToBytes(Maj23) };

    public override MessageType Type => MessageType.ConsensusMaj23Msg;

    public override Address Validator => Maj23.Validator;

    public override int Height => Maj23.Height;

    public override int Round => Maj23.Round;

    // /// <inheritdoc cref="ConsensusMsg.Equals(ConsensusMsg?)"/>
    // public override bool Equals(ConsensusMsg? other)
    // {
    //     return other is ConsensusMaj23Msg message &&
    //            message.Maj23.Equals(Maj23);
    // }

    // /// <inheritdoc cref="ConsensusMsg.Equals(object?)"/>
    // public override bool Equals(object? obj)
    // {
    //     return obj is ConsensusMaj23Msg other && Equals(other);
    // }

    /// <inheritdoc cref="ConsensusMsg.GetHashCode"/>
    // public override int GetHashCode()
    // {
    //     return HashCode.Combine(Type, Maj23);
    // }
}
