using Libplanet.Net.Consensus;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
public sealed record class ConsensusMaj23Message : ConsensusMessage
{
    [Property(0)]
    public required Maj23 Maj23 { get; init; }

    public override MessageType Type => MessageType.ConsensusMaj23Msg;

    public override Address Validator => Maj23.Validator;

    public override int Height => Maj23.Height;

    public override int Round => Maj23.Round;
}
