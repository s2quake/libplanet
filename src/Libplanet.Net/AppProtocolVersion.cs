using System.Diagnostics.Contracts;
using System.Globalization;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.Net;

public readonly struct AppProtocolVersion : IEquatable<AppProtocolVersion>
{
    public readonly int Version;

    public readonly byte[]? Extra;

    public readonly Address Signer;

    private readonly ImmutableArray<byte> _signature;

    public AppProtocolVersion(
        int version,
        byte[]? extra,
        ImmutableArray<byte> signature,
        Address signer)
    {
        Version = version;
        Extra = extra;
        _signature = signature;
        Signer = signer;
    }

    public ImmutableArray<byte> Signature =>
        _signature.IsDefault ? ImmutableArray<byte>.Empty : _signature;

    public string Token
    {
        get
        {
            throw new NotImplementedException();
            // string sig = Convert.ToBase64String(
            //     Signature.ToArray(),
            //     Base64FormattingOptions.None)
            // .Replace('/', '.');
            // var prefix =
            //     $"{Version.ToString(CultureInfo.InvariantCulture)}/{Signer:raw}/{sig}";
            // if (Extra is null)
            // {
            //     return prefix;
            // }

            // string extra = Convert.ToBase64String(
            //     _codec.Encode(Extra),
            //     Base64FormattingOptions.None)
            // .Replace('/', '.');
            // return $"{prefix}/{extra}";
        }
    }

    public static bool operator ==(AppProtocolVersion left, AppProtocolVersion right) =>
        left.Equals(right);

    public static bool operator !=(AppProtocolVersion left, AppProtocolVersion right) =>
        !(left == right);

    public static AppProtocolVersion Sign(PrivateKey signer, int version, byte[]? extra = null)
    {
        if (signer is null)
        {
            throw new ArgumentNullException(nameof(signer));
        }

        return new AppProtocolVersion(
            version,
            extra,
            ImmutableArray.Create(signer.Sign(GetMessage(version, extra))),
            new Address(signer.PublicKey));
    }

    public static AppProtocolVersion FromToken(string token)
    {
        throw new NotImplementedException();
        // if (token is null)
        // {
        //     throw new ArgumentNullException(nameof(token));
        // }

        // int pos, pos2;
        // pos = token.IndexOf('/');
        // if (pos < 0)
        // {
        //     throw new FormatException($"Failed to find the first field delimiter: {token}");
        // }

        // int version;
        // try
        // {
        //     version = int.Parse(token.Substring(0, pos), CultureInfo.InvariantCulture);
        // }
        // catch (Exception e) when (e is OverflowException || e is FormatException)
        // {
        //     throw new FormatException($"Failed to parse a version number: {token}", e);
        // }

        // pos++;
        // pos2 = token.IndexOf('/', pos);
        // if (pos2 < 0)
        // {
        //     throw new FormatException($"Failed to find the second field delimiter: {token}");
        // }

        // Address signer;
        // try
        // {
        //     signer = Address.Parse(token.Substring(pos, pos2 - pos));
        // }
        // catch (ArgumentException e)
        // {
        //     throw new FormatException($"Failed to parse a signer address: {token}", e);
        // }

        // pos2++;
        // pos = token.IndexOf('/', pos2);
        // string sigEncoded = pos < 0 ? token.Substring(pos2) : token.Substring(pos2, pos - pos2);

        // ImmutableArray<byte> sig;
        // try
        // {
        //     sig = ImmutableArray.Create(Convert.FromBase64String(sigEncoded.Replace('.', '/')));
        // }
        // catch (FormatException e)
        // {
        //     throw new FormatException($"Failed to parse a signature: {token}", e);
        // }

        // byte[]? extra = null;
        // if (pos >= 0)
        // {
        //     pos++;
        //     string extraEncoded = token.Substring(pos);
        //     byte[] extraBytes = Convert.FromBase64String(extraEncoded.Replace('.', '/'));
        //     try
        //     {
        //         extra = _codec.Decode(extraBytes);
        //     }
        //     catch (DecodingException e)
        //     {
        //         throw new FormatException(
        //             $"Failed to parse extra data (offset = {pos}): {token}", e);
        //     }
        // }

        // return new AppProtocolVersion(version, extra, sig, signer);
    }

    public bool Verify(PublicKey publicKey) =>
        Signer.Equals(new Address(publicKey)) &&
        publicKey.Verify(GetMessage(Version, Extra), Signature.ToBuilder().ToArray());

    [Pure]
    public bool Equals(AppProtocolVersion other) =>
    /* The reason why we need to check other fields than the Signature is that
    this struct in itself does not guarantee its Signature is derived from
    other field values.  A value of this struct can represent an invalid claim. */
    Version == other.Version &&
    Signer.Equals(other.Signer) &&
    Equals(Extra, other.Extra) &&
    Signature.SequenceEqual(other.Signature);

    [Pure]
    public override bool Equals(object? obj) =>
    obj is AppProtocolVersion other && Equals(other);

    [Pure]
    public override int GetHashCode()
    {
        int hash = 17;
        unchecked
        {
            hash *= 31 + Version.GetHashCode();
            hash *= 31 + (Extra is null ? 0 : Extra.GetHashCode());
            hash *= 31 + ByteUtility.CalculateHashCode(Signature.ToArray());
            hash *= 31 + Signer.GetHashCode();
        }

        return hash;
    }

    [Pure]
    public override string ToString() => string.Format(
    CultureInfo.InvariantCulture,
    Extra is null ? "{0}" : "{0} ({1})",
    Version,
    Extra);

    private static byte[] GetMessage(int version, byte[]? extra)
    {
        throw new NotImplementedException();
        // var list = new List(
        //     new Integer(version),
        //     extra is null ? null : extra);
        // return ByteUtility.CreateMessage(list);
    }
}
