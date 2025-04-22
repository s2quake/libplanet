using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Libplanet.Common;
using Libplanet.Crypto.Converters;
using Libplanet.Crypto.JsonConverters;
using Secp256k1Net;

namespace Libplanet.Crypto;

[TypeConverter(typeof(PublicKeyTypeConverter))]
[JsonConverter(typeof(PublicKeyJsonConverter))]
public sealed record class PublicKey : IEquatable<PublicKey>, IFormattable
{
    private readonly ImmutableArray<byte> _bytes;

    public PublicKey(ImmutableArray<byte> bytes)
        : this(bytes, verify: true)
    {
    }

    internal PublicKey(ImmutableArray<byte> bytes, bool verify)
    {
        if (verify)
        {
            if (!TryGetPublicKey(bytes, out var publicKey))
            {
                throw new ArgumentException("Invalid public key bytes.", nameof(bytes));
            }

            _bytes = [.. publicKey];
        }
        else
        {
            _bytes = bytes;
        }
    }

    public Address Address => new(this);

    internal ImmutableArray<byte> Raw => _bytes;

    public static PublicKey Parse(string hex) => new([.. ByteUtil.ParseHex(hex)], verify: true);

    public static bool Verify(
        Address signer, ImmutableArray<byte> message, ImmutableArray<byte> signature)
    {
        if (signature.IsDefaultOrEmpty)
        {
            return false;
        }

        try
        {
            return CryptoConfig.CryptoBackend.Verify([.. message], [.. signature], signer);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Equals(PublicKey? other)
        => other is not null && _bytes.SequenceEqual(other._bytes);

    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (byte @byte in _bytes)
        {
            hash.Add(@byte);
        }

        return hash.ToHashCode();
    }

    public ImmutableArray<byte> ToImmutableArray(bool compress)
        => [.. ToByteArray(compress)];

    public byte[] ToByteArray() => ToByteArray(compress: false);

    public byte[] ToByteArray(bool compress) => PrivateKey.GetPublicKey(_bytes, compress);

    public byte[] Encrypt(byte[] message)
    {
        var disposablePrivateKey = new PrivateKey();
        var aes = disposablePrivateKey.ExchangeKey(this);

        return aes.Encrypt(message, disposablePrivateKey.PublicKey.ToByteArray(true));
    }

    public ImmutableArray<byte> Encrypt(ImmutableArray<byte> message)
        => [.. Encrypt(message.ToBuilder().ToArray())];

    public bool Verify(IReadOnlyList<byte> message, IReadOnlyList<byte> signature)
        => Verify(Address, [.. message], [.. signature]);

    public override string ToString() => ByteUtil.Hex(ToByteArray(compress: false));

    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "c" => ByteUtil.Hex(ToByteArray(compress: true)),
        _ => ToString(),
    };

    private static bool TryGetPublicKey(
        ImmutableArray<byte> bytes, [MaybeNullWhen(false)] out byte[] publicKey)
    {
        lock (PrivateKey._secpLock)
        {
            using var secp256k1 = new Secp256k1();
            publicKey = new byte[Secp256k1.PUBKEY_LENGTH];
            if (secp256k1.PublicKeyParse(publicKey, bytes.ToArray()))
            {
                return true;
            }

            publicKey = null;
            return false;
        }
    }
}
