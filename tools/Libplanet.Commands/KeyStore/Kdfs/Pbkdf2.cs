using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text.Json;
using Libplanet.Types;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Libplanet.KeyStore.Kdfs;

/// <summary>
/// <a href="https://en.wikipedia.org/wiki/PBKDF2">PBKDF2</a>.
/// </summary>
/// <typeparam name="T">PRF (pseudorandom function) to use, e.g.,
/// <see cref="Sha256Digest"/>.</typeparam>
public sealed class Pbkdf2<T> : IKdf
    where T : GeneralDigest, new()
{
    public Pbkdf2(int iterations, byte[] salt, int keyLength)
        : this(iterations, ImmutableArray.Create(salt, 0, salt.Length), keyLength)
    {
    }

    public Pbkdf2(int iterations, in ImmutableArray<byte> salt, int keyLength)
    {
        Iterations = iterations;
        Salt = salt;
        KeyLength = keyLength;
    }

    public int Iterations { get; }

    public int KeyLength { get; }

    public ImmutableArray<byte> Salt { get; }

    public string Name => "pbkdf2";

    public ImmutableArray<byte> Derive(string passphrase)
    {
        var pdb = new Pkcs5S2ParametersGenerator(new T());
        pdb.Init(
            PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(passphrase.ToCharArray()),
            Salt.ToArray(),
            Iterations);
        var key = (KeyParameter)pdb.GenerateDerivedMacParameters(KeyLength * 8);
        return ImmutableArray.Create(key.GetKey(), 0, KeyLength);
    }

    public string WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("c", Iterations);
        writer.WriteNumber("dklen", KeyLength);
        string alg = new T().AlgorithmName;
        writer.WriteString(
            "prf",
            "hmac-" + alg.ToLower(CultureInfo.InvariantCulture).Replace("-", string.Empty));
        writer.WriteString("salt", ByteUtility.Hex(Salt));
        writer.WriteEndObject();
        return "pbkdf2";
    }

    public dynamic ToDynamic() => new
    {
        c = Iterations,
        dklen = KeyLength,
        prf = "hmac-" + new T().AlgorithmName.ToLower(CultureInfo.InvariantCulture).Replace("-", string.Empty),
        salt = ByteUtility.Hex(Salt)
    };

    public static Pbkdf2<T> FromDynamic(dynamic @dynamic)
    {
        if (@dynamic.c is not int iterations)
        {
            throw new ArgumentException("The \"c\" field must be an integer.", nameof(@dynamic));
        }

        if (@dynamic.dklen is not int keyLength)
        {
            throw new ArgumentException("The \"dklen\" field must be an integer.", nameof(@dynamic));
        }

        if (@dynamic.salt is not string saltString)
        {
            throw new ArgumentException("The \"salt\" field must be a string.", nameof(@dynamic));
        }

        if (@dynamic.prf is not string prf)
        {
            throw new ArgumentException("The \"prf\" field must be a string.", nameof(@dynamic));
        }

        var salt = ByteUtility.ParseHex(saltString);

        return prf switch
        {
            "hmac-sha256" => new Pbkdf2<T>(iterations, salt, keyLength),
            _ => throw new NotSupportedException($"Unsupported \"prf\" type: \"{prf}\"."),
        };
    }
}

internal static class Pbkdf2
{
    internal static IKdf FromJson(in JsonElement element)
    {
        if (!element.TryGetProperty("c", out JsonElement c))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"c\" field, the number of iterations.");
        }

        if (c.ValueKind != JsonValueKind.Number || !c.TryGetInt32(out int iterations))
        {
            throw new InvalidOperationException(
                "The \"c\" field, the number of iterations, must be a number.");
        }

        if (!element.TryGetProperty("dklen", out JsonElement dklen))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"dklen\" field, " +
                "the length of key in bytes.");
        }

        if (dklen.ValueKind != JsonValueKind.Number ||
            !dklen.TryGetInt32(out int keyLength))
        {
            throw new InvalidOperationException(
                "The \"dklen\" field, the length of key in bytes, must be a number.");
        }

        if (!element.TryGetProperty("salt", out JsonElement saltElement))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"salt\" field.");
        }

        string saltString;
        try
        {
            saltString = saltElement.GetString();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("The \"salt\" field must be a string.");
        }

        byte[] salt;
        try
        {
            salt = ByteUtility.ParseHex(saltString);
        }
        catch (ArgumentNullException)
        {
            throw new InvalidOperationException(
                "The \"salt\" field must not be null, but a string.");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                "The \"salt\" field must be a hexadecimal string of bytes.\n" + e);
        }

        if (!element.TryGetProperty("prf", out JsonElement prfElement))
        {
            throw new InvalidOperationException(
                "The \"kdfparams\" field must have a \"prf\" field.");
        }

        string prf;
        try
        {
            prf = prfElement.GetString();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                "The \"prf\" field must be a string.");
        }

        return prf switch
        {
            "hmac-sha256" => new Pbkdf2<Sha256Digest>(iterations, salt, keyLength),
            null =>
                throw new InvalidOperationException(
                    "The \"prf\" field must not be null, but a string."),
            _ =>
                throw new NotSupportedException($"Unsupported \"prf\" type: \"{prf}\"."),
        };
    }
}
