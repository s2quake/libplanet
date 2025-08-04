namespace Libplanet.Types;

public interface ISigner
{
    Address Address { get; }

    byte[] Sign(Span<byte> message);
}
