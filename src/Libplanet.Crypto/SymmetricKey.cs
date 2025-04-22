using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Libplanet.Common;

namespace Libplanet.Crypto;

public sealed record class SymmetricKey(ImmutableArray<byte> Key) : IEquatable<SymmetricKey>
{
    private const int KeyByteSize = 32;
    private const int TagByteSize = 16;
    private const int NonceByteSize = 12;

    private readonly RandomNumberGenerator _secureRandom = RandomNumberGenerator.Create();

    public SymmetricKey(Span<byte> key)
        : this(ValidateKey(key.ToImmutableArray()))
    {
    }

    public ImmutableArray<byte> Key { get; } = ValidateKey(Key);

    public bool Equals(SymmetricKey? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Key.SequenceEqual(other.Key);
    }

    public override int GetHashCode() => ByteUtil.CalculateHashCode(ToByteArray());

    public byte[] Encrypt(byte[] message, byte[] nonSecret)
    {
#if NETSTANDARD2_0_OR_GREATER
        using var aes = new AesGcm(_key);
#else
        using var aes = new AesGcm([.. Key], tagSizeInBytes: TagByteSize);
#endif
        var nonce = new byte[NonceByteSize];
        _secureRandom.GetBytes(nonce);

        var ciphertext = new byte[message.Length];
        var tag = new byte[TagByteSize];
        _secureRandom.GetBytes(nonce);

        aes.Encrypt(nonce, message, ciphertext, tag, nonSecret);

        using var resultStream = new MemoryStream();
        using var writer = new BinaryWriter(resultStream);
        writer.Write(nonSecret);
        writer.Write(nonce);
        writer.Write(ciphertext);
        writer.Write(tag);
        return resultStream.ToArray();
    }

    public byte[] Decrypt(byte[] ciphertext, int nonSecretLength = 0)
    {
#if NETSTANDARD2_0_OR_GREATER
        using var aes = new AesGcm(_key);
#else
        using var aes = new AesGcm([.. Key], tagSizeInBytes: TagByteSize);
#endif
        using var inputStream = new MemoryStream(ciphertext);
        using var reader = new BinaryReader(inputStream);
        var nonSecretPayload = reader.ReadBytes(nonSecretLength);

        var nonce = reader.ReadBytes(NonceByteSize);
        var encryptedMessage = reader.ReadBytes(
            ciphertext.Length - nonSecretLength - nonce.Length - TagByteSize);
        var tag = reader.ReadBytes(TagByteSize);
        var decryptedMessage = new byte[encryptedMessage.Length];
        try
        {
            aes.Decrypt(nonce, encryptedMessage, tag, decryptedMessage, nonSecretPayload);
            return decryptedMessage;
        }
        catch (Exception e)
        {
            var message = "Failed to decrypt the ciphertext.";
            throw new InvalidCiphertextException(message, e);
        }
    }

    public byte[] ToByteArray() => [.. Key];

    private static ImmutableArray<byte> ValidateKey(ImmutableArray<byte> key)
    {
        if (key == null || key.Length != KeyByteSize)
        {
            throw new ArgumentException(
                $"Key needs to be {KeyByteSize} bytes!",
                nameof(key));
        }

        return key;
    }
}
