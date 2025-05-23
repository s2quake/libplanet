using System.Diagnostics.Contracts;
using System.Globalization;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Crypto;

namespace Libplanet.Net;

[Model(Version = 1)]
public readonly record class Protocol
{
    [Property(0)]
    public required ProtocolMetadata Metadata { get; init; }

    [Property(1)]
    public required ImmutableArray<byte> Signature; { get; init; }

    public Address Signer => Metadata.Signer;

    // public Protocol(
    //     int version,
    //     byte[]? extra,
    //     ImmutableArray<byte> signature,
    //     Address signer)
    // {
    //     Version = version;
    //     Extra = extra;
    //     Signature = signature;
    //     Signer = signer;
    // }

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

    public static Protocol FromToken(string token)
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

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata).ToImmutableArray();
        return Signer.Verify(bytes, Signature);
    }
}
