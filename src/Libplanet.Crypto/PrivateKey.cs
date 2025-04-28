using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Libplanet.Common;
using Secp256k1Net;

namespace Libplanet.Crypto;

public sealed record class PrivateKey(in ImmutableArray<byte> Bytes) : IEquatable<PrivateKey>
{
    internal static readonly object _lock = new();
    private const int KeyByteSize = 32;
    private PublicKey? _publicKey;

    public PrivateKey()
        : this(GenerateBytes())
    {
    }

    public PrivateKey(ReadOnlySpan<byte> bytes)
        : this(ValidateBytes(bytes.ToImmutableArray()))
    {
    }

    public ImmutableArray<byte> Bytes { get; } = ValidateBytes(Bytes);

    public PublicKey PublicKey
    {
        get
        {
            if (_publicKey is null)
            {
                lock (_lock)
                {
                    using var secp256k1 = new Secp256k1();
                    var publicKey = new byte[Secp256k1.PUBKEY_LENGTH];
                    secp256k1.PublicKeyCreate(publicKey, Bytes.ToArray());
                    _publicKey = new PublicKey([.. publicKey], verify: false);
                }
            }

            return _publicKey;
        }
    }

    public Address Address => new(PublicKey);

    public static PrivateKey Parse(string hex)
    {
        if (hex.Length != KeyByteSize * 2)
        {
            throw new FormatException(
                $"Expected {KeyByteSize * 2} hexadecimal characters, but got {hex.Length}.");
        }

        try
        {
            return new(ByteUtil.ParseHexToImmutable(hex));
        }
        catch (Exception e)
        {
            throw new FormatException($"Invalid hexadecimal string: {hex}", e);
        }
    }

    public bool Equals(PrivateKey? other)
        => other is not null && Bytes.SequenceEqual(other.Bytes);

    public override int GetHashCode()
    {
        HashCode hash = default;
        for (var i = 0; i < Bytes.Length; i++)
        {
            hash.Add(Bytes[i]);
        }

        return hash.ToHashCode();
    }

    public byte[] Sign(byte[] message) => CryptoConfig.CryptoBackend.Sign(message, this);

    public ImmutableArray<byte> Sign(ImmutableArray<byte> message)
    {
        var signature = CryptoConfig.CryptoBackend.Sign([.. message], this);
        return Unsafe.As<byte[], ImmutableArray<byte>>(ref signature);
    }

    public byte[] Decrypt(byte[] ciphertext)
    {
        var publicKey = new PublicKey([.. ciphertext.Take(33)]);
        var aes = ExchangeKey(publicKey);
        return aes.Decrypt(ciphertext, 33);
    }

    public ImmutableArray<byte> Decrypt(ImmutableArray<byte> ciphertext)
        => [.. Decrypt(ciphertext.ToBuilder().ToArray())];

    public SymmetricKey ExchangeKey(PublicKey publicKey)
    {
        lock (_lock)
        {
            using var secp256k1 = new Secp256k1();
            var secret = new byte[Secp256k1.SECRET_LENGTH];
            secp256k1.Ecdh(secret, publicKey.Bytes.ToArray(), Bytes.ToArray());

            return new SymmetricKey(secret);
        }
    }

    internal static byte[] GetPublicKey(in ImmutableArray<byte> raw, bool compress)
    {
        lock (_lock)
        {
            using var secp256k1 = new Secp256k1();
            var length = compress
                ? Secp256k1.SERIALIZED_COMPRESSED_PUBKEY_LENGTH
                : Secp256k1.SERIALIZED_UNCOMPRESSED_PUBKEY_LENGTH;
            var flag = compress
                ? Flags.SECP256K1_EC_COMPRESSED : Flags.SECP256K1_EC_UNCOMPRESSED;
            var publicKey = new byte[length];
            if (!secp256k1.PublicKeySerialize(publicKey, raw.ToArray(), flag))
            {
                throw new NotSupportedException("Failed to serialize a public key.");
            }

            return publicKey;
        }
    }

    private static ImmutableArray<byte> GenerateBytes()
    {
        lock (_lock)
        {
            using var secp256k1 = new Secp256k1();
            using var rnd = RandomNumberGenerator.Create();
            var privateKey = new byte[Secp256k1.PRIVKEY_LENGTH];
            do
            {
                rnd.GetBytes(privateKey);
            }
            while (!secp256k1.SecretKeyVerify(privateKey));
            return [.. privateKey];
        }
    }

    private static ImmutableArray<byte> ValidateBytes(in ImmutableArray<byte> bytes)
    {
        lock (_lock)
        {
            using var secp256k1 = new Secp256k1();
            if (bytes.Length != KeyByteSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bytes),
                    $"Private key needs to be {KeyByteSize} bytes!");
            }

            if (!secp256k1.SecretKeyVerify(bytes.ToArray()))
            {
                throw new ArgumentException("Invalid private key.", nameof(bytes));
            }

            return bytes;
        }
    }
}
