using Libplanet.Types.Crypto;
using Secp256k1Net;

namespace Libplanet.Types;

public interface ISigner
{
    Address Address { get; }

    byte[] Sign(Span<byte> message);
}
