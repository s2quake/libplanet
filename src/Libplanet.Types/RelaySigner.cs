
namespace Libplanet.Types;

public sealed class RelaySigner(PrivateKey privateKey) : ISigner
{
    Address ISigner.Address => privateKey.Address;

    byte[] ISigner.Sign(Span<byte> message) => privateKey.Sign(message);
}
