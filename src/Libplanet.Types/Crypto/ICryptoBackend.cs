namespace Libplanet.Types.Crypto;

public interface ICryptoBackend
{
    byte[] Sign(ReadOnlySpan<byte> message, PrivateKey privateKey);

    bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, Address signer);
}
