using System;
using Libplanet.Crypto;

namespace Libplanet.Types.Tx;

public interface ITxSigningMetadata : IEquatable<ITxSigningMetadata>
{
    long Nonce { get; }

    Address Signer { get; }
}
