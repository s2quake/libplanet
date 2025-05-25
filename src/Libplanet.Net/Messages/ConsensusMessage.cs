using Libplanet.Types.Crypto;

namespace Libplanet.Net.Messages;

public abstract record class ConsensusMessage : MessageContent
{
    public abstract Address Validator { get; }

    public abstract int Height { get; }

    public abstract int Round { get; }
}
