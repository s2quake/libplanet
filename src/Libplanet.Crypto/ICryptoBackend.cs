namespace Libplanet.Crypto;

public interface ICryptoBackend
{
    byte[] Sign(byte[] message, PrivateKey privateKey);

    bool Verify(byte[] message, byte[] signature, Address signer);
}
