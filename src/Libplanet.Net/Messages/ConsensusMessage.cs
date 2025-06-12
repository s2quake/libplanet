using Libplanet.Types;

namespace Libplanet.Net.Messages;

public abstract record class ConsensusMessage : MessageBase
{
    public abstract Address Validator { get; }

    public abstract int Height { get; }

    public abstract int Round { get; }
}
