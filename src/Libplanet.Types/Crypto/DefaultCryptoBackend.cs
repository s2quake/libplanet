using System.Text;
using Nethereum.Util;
using Secp256k1Net;

namespace Libplanet.Types.Crypto;

public sealed class DefaultCryptoBackend : ICryptoBackend
{
    private const int Offset = 27;

    public byte[] Sign(byte[] message, PrivateKey privateKey)
    {
        lock (PrivateKey._lock)
        {
            using var secp = new Secp256k1();
            var messageHash = HashPrefixedMessage(message);
            var keyBytes = privateKey.Bytes.ToArray();
            var sig1 = new byte[Secp256k1.UNSERIALIZED_SIGNATURE_SIZE];
            var sig2 = new byte[Secp256k1.SERIALIZED_SIGNATURE_SIZE];
            var signature = new byte[Secp256k1.UNSERIALIZED_SIGNATURE_SIZE];
            if (!secp.SignRecoverable(sig1, messageHash, keyBytes))
            {
                throw new InvalidOperationException("Failed to sign the message.");
            }

            if (!secp.RecoverableSignatureSerializeCompact(sig2, out var recoverId, sig1))
            {
                throw new InvalidOperationException("Failed to serialize the signature.");
            }

            Array.Copy(sig2, signature, sig2.Length);
            signature[sig2.Length] = Convert.ToByte(recoverId + Offset);
            return signature;
        }
    }

    public bool Verify(byte[] message, byte[] signature, PublicKey publicKey)
        => Verify(message, signature, publicKey.Address);

    public bool Verify(byte[] message, byte[] signature, Address signer)
    {
        lock (PrivateKey._lock)
        {
            using var secp = new Secp256k1();
            var publicKeyOutput = new byte[Secp256k1.PUBKEY_LENGTH];
            var serializedKey = new byte[Secp256k1.SERIALIZED_UNCOMPRESSED_PUBKEY_LENGTH];
            var messageHash = HashPrefixedMessage(message);
            var recoverId = signature[signature.Length - 1] - Offset;

            var sig2 = new byte[Secp256k1.UNSERIALIZED_SIGNATURE_SIZE];
            if (!secp.RecoverableSignatureParseCompact(sig2, signature, recoverId))
            {
                return false;
            }

            if (!secp.Recover(publicKeyOutput, sig2, messageHash))
            {
                return false;
            }

            if (!secp.PublicKeySerialize(serializedKey, publicKeyOutput))
            {
                return false;
            }

            return signer.Equals(new PublicKey([.. serializedKey]).Address);
        }
    }

    private static byte[] HashPrefixedMessage(byte[] message)
    {
        var bytePrefix = ByteUtility.ParseHex("19");
        var textBytePrefix = Encoding.UTF8.GetBytes(
            "Ethereum Signed Message:\n" + message.Length);
        var byteList = new List<byte>(
            bytePrefix.Length + textBytePrefix.Length + message.Length);

        byteList.AddRange(bytePrefix);
        byteList.AddRange(textBytePrefix);
        byteList.AddRange(message);
        return new DefaultMessageHasher().Hash(byteList.ToArray());
    }
}
